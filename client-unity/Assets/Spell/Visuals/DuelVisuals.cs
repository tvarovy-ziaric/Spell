using System;
using System.Collections.Generic;
using Spell.Client.Preflight;
using Spell.Geometry;
using UnityEngine;

namespace Spell.Client.Visuals
{
    [DefaultExecutionOrder(150)]
    public sealed class DuelVisuals : MonoBehaviour
    {
        sealed class Flight
        {
            public Vector3 Start, End;
            public double From, To;
            public bool Impact;
            public IFireballRenderer View;
        }

        readonly Dictionary<ulong,Flight> flights = new Dictionary<ulong,Flight>();
        readonly List<ulong> expired = new List<ulong>();
        public event Action CastRequested;
        VisualBudget budget;
        bool trajectory, cameraOpen, haveAim, terrainHit;
        VisualModule<IMarkerVisualTheme> markerModule=VisualModules.InfernoTheme;
        VisualModule<IFireballRenderer> fireballModule=VisualModules.Ember;
        int profile;
        MarkerPresentation markers;
        VisualArt guide;
        TerrainDebugPatch terrainDebug;
        ArenaSurface debugSurface;
        ArenaRegistration debugRegistration;
        bool showTerrain;
        LineRenderer ray, ring;
        Vector3 source, end;
        readonly Vector3[] footprint = new Vector3[48];
        bool footprintKnown;
        bool heatLimited;
        float radius;
        public int FrameLimit => budget.FramesPerSecond;
        public string MarkerThemeName => markerModule.Name;
        public string FireballStyleName => fireballModule.Name;
        public bool ThermalLimited => heatLimited;
        public bool HasAim => haveAim;
        public int VisibleFlights => flights.Count;

        void Awake() => ApplyProfile(0);
        void ApplyProfile(int value)
        {
            profile = value;
            budget = heatLimited || value == 1 ? new VisualBudget(true) : value == 2 ?
                new VisualBudget(false) : VisualBudget.Auto(SystemInfo.systemMemorySize, SystemInfo.graphicsMemorySize);
            Application.targetFrameRate = budget.FramesPerSecond;
            ReplaceMarkerTheme();
            ReplaceFlightStyle();
        }

        public void ApplyThermalStatus(int status)
        {
            // Android THERMAL_STATUS_SEVERE or above; apply only to cosmetic work.
            bool limited = status >= 3;
            if (limited == heatLimited) return;
            heatLimited = limited;
            ApplyProfile(profile);
        }

        public bool Predict(ArenaSurface arena, ArenaRegistration registration, MarkerCalibration frame,
            Pose view, float range, float aoeRadius)
        {
            if (arena == null || registration == null || frame == null || !VisualContract.Valid(view) ||
                !VisualContract.Finite(aoeRadius) || aoeRadius < 0)
            { ClearAim(); return false; }
            var start=registration.ToArena(frame,view.position);
            var dir=registration.DirectionToArena(frame,view.rotation*Vector3.forward);
            var path=FireballPath.Trace(arena,start.x,start.y,start.z,dir.x,dir.y,dir.z,range);
            if (path.EndKind==FlightEndKind.Unknown) { ClearAim(); return false; }
            var impact = new Vector3((float)path.EndEast,(float)path.EndUp,(float)path.EndNorth);
            SetAim(view.position,registration.ToTracking(frame,impact),aoeRadius,path.EndKind==FlightEndKind.Terrain);
            if (terrainHit)
            {
                footprintKnown = true;
                for (int i=0; i<footprint.Length; i++)
                {
                    float a=i*2*Mathf.PI/footprint.Length;
                    float east=impact.x+aoeRadius*Mathf.Cos(a), north=impact.z+aoeRadius*Mathf.Sin(a);
                    if (!arena.TryHeight(east,north,out var height)) { footprintKnown=false; break; }
                    footprint[i]=registration.ToTracking(frame,new Vector3(east,(float)height+.015f,north));
                }
            }
            return true;
        }

        public void SetAim(Vector3 trackingOrigin, Vector3 trackingEnd, float aoeMeters, bool hit)
        {
            if (!VisualContract.Finite(trackingOrigin) || !VisualContract.Finite(trackingEnd) ||
                !VisualContract.Finite(aoeMeters) || aoeMeters < 0) { ClearAim(); return; }
            source = trackingOrigin; end = trackingEnd; radius = aoeMeters; terrainHit = hit; haveAim = true;
            footprintKnown = false;
        }

        public void ClearAim()
        {
            haveAim = false;
            guide?.Hide();
        }

        // Snapshot replacement corrects time and endpoints on reconnect; duplicates reuse the same visual.
        public void ShowFireball(ulong id, Vector3 start, Vector3 finish, double startRealtime, double endRealtime)
        {
            if (!VisualContract.Finite(start) || !VisualContract.Finite(finish) ||
                !VisualContract.Finite(startRealtime) || !VisualContract.Finite(endRealtime) || endRealtime < startRealtime) return;
            if (!flights.TryGetValue(id, out Flight flight))
            {
                if (flights.Count >= 128) return;
                flight = new Flight();
                flights.Add(id, flight);
            }
            if (flight.Impact) return;
            flight.Start=start; flight.End=finish; flight.From=startRealtime; flight.To=endRealtime;
        }

        public void ConfirmImpact(ulong id, Vector3 position, double realtime)
        {
            if (!VisualContract.Finite(position) || !VisualContract.Finite(realtime)) return;
            if (!flights.TryGetValue(id,out Flight f))
            {
                if (flights.Count >= 128) return;
                f=new Flight();
                flights.Add(id,f);
            }
            f.Start=f.End=position; f.From=realtime; f.To=realtime+.32; f.Impact=true;
        }

        public void ClearFlights()
        {
            foreach (Flight flight in flights.Values) flight.View?.Dispose();
            flights.Clear();
        }

        public void SetCameraOpen(bool visible) => cameraOpen=visible;
        public void UpdateTerrainDebug(ArenaSurface surface, ArenaRegistration registration, BoardCalibration boards, Pose view)
        {
            if (!cameraOpen || !showTerrain || surface == null || boards == null ||
                !boards.TryGetSharedFrame(out var frame) || !VisualContract.Valid(view))
            { terrainDebug?.Hide(); return; }
            if (debugSurface!=surface)
            {
                debugSurface=surface;
                debugRegistration=registration;
            }
            if (debugRegistration==null) { terrainDebug?.Hide(); return; }
            terrainDebug ??= new TerrainDebugPatch();
            terrainDebug.Update(surface,debugRegistration,frame,view.position);
        }
        public void SelectFireball(VisualModule<IFireballRenderer> next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            next.Validate();
            if (fireballModule == next) return;
            fireballModule=next;
            ReplaceFlightStyle();
        }
        public void SelectMarkers(VisualModule<IMarkerVisualTheme> next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            next.Validate();
            if (next == markerModule) return;
            markers?.Select(next.Create(budget));
            markerModule=next;
        }
        void ReplaceMarkerTheme() => markers?.Select(markerModule.Create(budget));
        void ReplaceFlightStyle()
        {
            foreach (Flight flight in flights.Values)
            {
                flight.View?.Dispose();
                flight.View=null;
            }
        }

        void LateUpdate()
        {
            if (markers == null && TryGetComponent(out BoardCalibration boards))
                markers = new MarkerPresentation(boards,markerModule.Create(budget));
            markers?.Draw(Time.unscaledTime);
            DrawFlights(Time.realtimeSinceStartupAsDouble);
            DrawGuide();
        }

        public void DrawFlights(double now)
        {
            expired.Clear();
            foreach (var row in flights)
            {
                Flight f = row.Value;
                if (now > f.To) { expired.Add(row.Key); continue; }
                if (!cameraOpen || now < f.From) { f.View?.Hide(); continue; }
                float alpha = f.To == f.From ? 1 : Mathf.Clamp01((float)((now-f.From)/(f.To-f.From)));
                f.View ??= fireballModule.Create(budget);
                var heading = f.End-f.Start;
                f.View.Present(Vector3.Lerp(f.Start,f.End,alpha),heading.sqrMagnitude>1e-10f ? heading.normalized : Vector3.forward,
                    (float)(now-f.From),f.Impact ? alpha : -1);
            }
            foreach (var id in expired) { flights[id].View?.Dispose(); flights.Remove(id); }
        }

        void DrawGuide()
        {
            if (!cameraOpen || !haveAim || (!trajectory && !terrainHit)) { guide?.Hide(); return; }
            if (guide == null)
            {
                guide=new VisualArt("Predpokladaná trajektória a zóna dopadu");
                ray=Line("Trajektória",2,false);
                ring=Line("Predpokladaný dopad",48,true);
            }
            guide.Root.gameObject.SetActive(true);
            ray.enabled=trajectory;
            if (trajectory) { ray.SetPosition(0,source); ray.SetPosition(1,end); }
            ring.enabled=terrainHit && radius>0 && footprintKnown;
            if (ring.enabled) ring.SetPositions(footprint);
        }

        LineRenderer Line(string name,int count,bool loop)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(guide.Root,false);
            line.sharedMaterial=guide.Material;
            guide.Colorize(line,new Color(.97f,.7f,.2f,.9f));
            line.loop=loop; line.useWorldSpace=true; line.positionCount=count;
            line.startWidth=line.endWidth=.018f;
            return line;
        }

        public void DrawSettings()
        {
            if (GUILayout.Button("Terčíky · "+markerModule.Name,GUILayout.MinHeight(44)))
                SelectMarkers(markerModule==VisualModules.InfernoTheme ? VisualModules.NecroTheme : VisualModules.InfernoTheme);
            if (GUILayout.Button("Fireball · "+fireballModule.Name,GUILayout.MinHeight(44)))
                SelectFireball(fireballModule==VisualModules.Ember ? VisualModules.Rune : VisualModules.Ember);
            string name=profile==0?"Automaticky":profile==1?"Úsporný":"Plynulý";
            if (GUILayout.Button($"Grafika · {name} ({FrameLimit} FPS)",GUILayout.MinHeight(44))) ApplyProfile((profile+1)%3);
            if (heatLimited) GUILayout.Label("Telefón sa zahrieva. Grafika beží v úspornom režime.");
            trajectory=GUILayout.Toggle(trajectory," Zobraziť trajektóriu");
            showTerrain=GUILayout.Toggle(showTerrain," Zobraziť terénnu sieť v kamere");
        }

        public void DrawSight(float width,float height)
        {
            float x=width*.5f, y=height*.5f;
            Color previous=GUI.color;
            GUI.color=haveAim?Color.white:Color.gray;
            GUI.DrawTexture(new Rect(x-10,y-1,6,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x+4,y-1,6,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x-1,y-10,2,6),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x-1,y+4,2,6),Texture2D.whiteTexture);
            GUI.color=previous;
            if (!haveAim) GUI.Label(new Rect(12,10,width-24,36),"Zameranie čaká na dôveryhodný herný rámec.");
            if (CastRequested != null)
            {
                bool enabled=GUI.enabled;
                GUI.enabled=enabled && haveAim;
                if (GUI.Button(new Rect(width-126,height-114,114,48),"Fireball")) CastRequested.Invoke();
                GUI.enabled=enabled;
            }
        }

        void OnDisable() { ClearAim(); ClearFlights(); markers?.Dispose(); markers=null; terrainDebug?.Hide(); }
        void OnDestroy() { guide?.Dispose(); terrainDebug?.Dispose(); }
    }
}
