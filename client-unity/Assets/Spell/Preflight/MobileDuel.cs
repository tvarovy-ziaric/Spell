using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Spell.Client.Diagnostics;
using Spell.Client.Visuals;
using Spell.Geometry;
using Spell.Protocol.V1;
using UnityEngine;

namespace Spell.Client.Preflight
{
    // All Unity state is read on the main thread. Only one send and one receive may be outstanding.
    public sealed class MobileDuel : MonoBehaviour
    {
        OnlineArenaDownload online;
        DeviceDiagnostics device;
        DuelVisuals visuals;
        ClientWebSocket socket;
        CancellationTokenSource lifetime;
        Task sending;
        string seat;
        Snapshot snapshot;
        ArenaRegistration registration;
        ArenaSurface surface;
        bool? ready;
        bool inBackground, cast;
        ulong lastPose, command, lastImpact;
        long lastSample;
        double retryAt, nextPing, lastReceipt;
        public bool HasStarted { get; private set; }
        public string Status { get; private set; } = "";

        void Start()
        {
            online = GetComponent<OnlineArenaDownload>();
            device = GetComponent<DeviceDiagnostics>();
            visuals = GetComponent<DuelVisuals>();
            visuals.CastRequested += RequestCast;
        }
        void RequestCast() { cast = snapshot?.Phase == Phase.Playing && snapshot.Combat?.Finished == false; }
        void Update()
        {
            string next = online.Arena == null ? null : online.SeatToken;
            if (next != seat)
            {
                StopSocket();
                seat = next;
                command = lastImpact = 0;
                HasStarted = false;
                retryAt = 0;
            }
            if (surface != online.Arena)
            {
                surface = online.Arena;
                registration = surface == null ? null : ArenaRegistration.ForArena(surface);
            }
            if (seat == null || inBackground) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (socket == null)
            {
                if (now >= retryAt) Connect();
                return;
            }
            if (socket.State != WebSocketState.Open) return;
            if (now - lastReceipt > 20) { StopSocket(); retryAt = now + 1; return; }
            if (sending != null && !sending.IsCompleted) return;
            if (sending != null && sending.IsFaulted) { StopSocket(); retryAt = now + 2; return; }
            var frameSource = device.GetComponent<BoardCalibration>();
            MarkerCalibration frame = null;
            bool calibrated = registration != null && frameSource != null && frameSource.TryGetSharedFrame(out frame)
                && device.Timing.HasRecentTracking;
            if (!calibrated || snapshot?.Phase != Phase.Playing || snapshot.Combat?.Version != 1 || snapshot.Combat?.Finished != false || !online.Rules.fireballEnabled)
                visuals.ClearAim();
            else
                visuals.Predict(surface, registration, frame, device.ViewPose, (float)online.Rules.range, (float)online.Rules.radius);
            if (ready != calibrated)
            {
                ready = calibrated;
                Send(new ClientFrame { Ready = new Ready { LoadedInRam = true, Calibrated = calibrated,
                    CombatVersion = 1, ArenaRevision = 1, ArenaSha256 = surface.Sha256, Preset = online.PresetId } });
                return;
            }
            if (cast)
            {
                cast = false;
                if (calibrated && visuals.HasAim && lastPose > 0 && snapshot?.Phase == Phase.Playing &&
                    !snapshot.Combat.Finished)
                {
                    Send(new ClientFrame { Cast = new CastFireball { CommandId = ++command, PoseSequence = lastPose } });
                    return;
                }
            }
            if (now >= nextPing)
            {
                nextPing = now + 5;
                Send(new ClientFrame { Ping = new Spell.Protocol.V1.Ping { ClientUs = (long)(now * 1e6) } });
                return;
            }
            var timing = device.Timing;
            if (!calibrated || timing.EstimatedUs <= lastSample) return;
            lastSample = timing.EstimatedUs;
            var camera = device.ViewPose;
            var pt = registration.ToArena(frame, camera.position);
            var dir = registration.DirectionToArena(frame, camera.rotation * Vector3.forward);
            var up = registration.DirectionToArena(frame, camera.rotation * Vector3.up);
            var rotation = Quaternion.LookRotation(dir, up);
            // Provisional board-fit uncertainty for the diagnostic fixture. Field accuracy is measured at M2.
            double residual = Math.Max(0.12, frame.MaxResidualMeters);
            var pose = new PoseSample { Sequence = ++lastPose, ArenaRevision = 1, EstimatedUs = timing.EstimatedUs,
                LastVisualUs = timing.VisualReceiptUs, Position = new Vector3M { East=pt.x, Up=pt.y, North=pt.z },
                Orientation = new Orientation { X=rotation.x,Y=rotation.y,Z=rotation.z,W=rotation.w },
                Tracking = Tracking.Good, PositionSigmaM = residual, AngleSigmaDeg = Math.Atan2(residual, 1.5)*180/Math.PI };
            if (timing.LastGnssUs > 0) pose.LastGnssUs = timing.LastGnssUs;
            Send(new ClientFrame { Pose = pose });
        }
        void Send(ClientFrame frame)
        {
            sending = SendFrame(socket, lifetime.Token, frame);
        }
        async Task SendFrame(ClientWebSocket ws, CancellationToken stop, ClientFrame frame)
        {
            await ws.SendAsync(new ArraySegment<byte>(frame.ToByteArray()), WebSocketMessageType.Binary, true, stop);
            if (frame.Pose != null && ws == socket) GetComponent<DiagnosticsUpload>()?.Sink?.Record(Measure.SentPose);
        }
        async void Connect()
        {
            var ws = new ClientWebSocket();
            var run = new CancellationTokenSource();
            socket = ws;
            lifetime = run;
            try
            {
                Status = "Pripájam herné spojenie…";
                ws.Options.SetRequestHeader("Authorization", "Bearer " + seat);
                run.CancelAfter(TimeSpan.FromSeconds(10));
                await ws.ConnectAsync(new Uri("wss://spell.arnavigator.site/v1/events"), run.Token);
                run.CancelAfter(Timeout.Infinite);
                lastReceipt = Time.realtimeSinceStartupAsDouble;
                var bytes = new byte[65536];
                while (!run.IsCancellationRequested)
                {
                    int total = 0;
                    WebSocketReceiveResult part;
                    do
                    {
                        if (total == bytes.Length) throw new InvalidDataException();
                        part = await ws.ReceiveAsync(new ArraySegment<byte>(bytes, total, bytes.Length-total),run.Token);
                        if (part.MessageType != WebSocketMessageType.Binary) return;
                        total += part.Count;
                    } while (!part.EndOfMessage);
                    if (ws != socket) return;
                    lastReceipt = Time.realtimeSinceStartupAsDouble;
                    var message = ServerFrame.Parser.ParseFrom(bytes,0,total);
                    if (message.Snapshot != null) Apply(message.Snapshot);
                    if (message.Pong != null)
                        RoundTripMs = Math.Max(0,Time.realtimeSinceStartupAsDouble*1000 - message.Pong.ClientUs/1000.0);
                }
            }
            catch (Exception) { /* Repeat the authenticated handshake using the existing seat. */ }
            finally
            {
                if (socket == ws) { StopSocket(); retryAt = Time.realtimeSinceStartupAsDouble + 2; }
                ws.Dispose(); run.Dispose();
            }
        }
        public double RoundTripMs { get; private set; }
        void Apply(Snapshot value)
        {
            snapshot = value;
            if (value.Phase == Phase.Playing) HasStarted = true;
            double now = Time.realtimeSinceStartupAsDouble;
            var combat = value.Combat;
            Status = value.Phase == Phase.Preparing ? "Herné spojenie pripravené. Čakám na kalibráciu oboch hráčov." :
                value.Phase == Phase.Paused ? "Súboj pozastavený." : "Súboj prebieha.";
            if (combat == null || combat.Version != 1) { visuals.ClearFlights(); return; }
            command = Math.Max(command, online.IsFounder ? combat.FounderCommand : combat.GuestCommand);
            Status += $" HP {combat.FounderHp} / {combat.GuestHp}. Aura: {combat.Safety}.";
            if (combat.Finished) Status = $"Súboj sa skončil. HP {combat.FounderHp} / {combat.GuestHp}.";
            var boards = GetComponent<BoardCalibration>();
            if (registration == null || boards == null || !boards.TryGetSharedFrame(out var frame) || value.Phase != Phase.Playing)
            { visuals.ClearFlights(); return; }
            Vector3 ToView(Vector3M p) => registration.ToTracking(frame,new Vector3((float)p.East,(float)p.Up,(float)p.North));
            foreach (var f in combat.Flights)
            {
                double from = now + (f.StartGameUs - combat.GameUs)/1e6;
                visuals.ShowFireball(f.Id,ToView(f.Start),ToView(f.End),from,from+f.DurationUs/1e6);
            }
            foreach (var impact in combat.Impacts)
            {
                if (impact.Id <= lastImpact) continue;
                if (combat.GameUs - impact.GameUs <= 300000) visuals.ConfirmImpact(impact.Id,ToView(impact.Position),now);
                lastImpact = Math.Max(lastImpact,impact.Id);
            }
        }
        void StopSocket()
        {
            var old = socket;
            socket = null;
            lifetime?.Cancel();
            lifetime = null;
            old?.Abort();
            if (sending?.IsFaulted == true) _ = sending.Exception;
            sending = null; ready = null; snapshot = null; cast = false; lastPose = 0; lastSample = 0;
            visuals?.ClearFlights(); visuals?.ClearAim();
            Status = seat == null ? "" : "Herné spojenie sa obnovuje…";
        }
        void OnApplicationPause(bool paused) { inBackground = paused; if (paused) StopSocket(); }
        void OnDestroy() { if (visuals != null) visuals.CastRequested -= RequestCast; StopSocket(); }
    }
}
