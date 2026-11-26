using System;

namespace Spell.Client.Visuals
{
    // Compiled-in modules only. Select an implementation without modifying gameplay or localization.
    public sealed class VisualModule<T> where T : class, IDisposable
    {
        public string Name { get; }
        public int ApiVersion { get; }
        readonly Func<VisualBudget,T> create;
        public VisualModule(string name, int apiVersion, Func<VisualBudget,T> factory)
        {
            Name=name ?? throw new ArgumentNullException(nameof(name));
            ApiVersion=apiVersion;
            create=factory ?? throw new ArgumentNullException(nameof(factory));
        }
        public void Validate()
        {
            if (ApiVersion != VisualContract.Version)
                throw new ArgumentException("Unsupported visual contract version.");
        }
        public T Create(VisualBudget budget)
        {
            Validate();
            var result=create(budget);
            int v=result is IMarkerVisualTheme marker ? marker.ApiVersion :
                result is IFireballRenderer spell ? spell.ApiVersion : 0;
            if (v != ApiVersion)
            {
                result?.Dispose();
                throw new InvalidOperationException("Module implementation and descriptor versions differ.");
            }
            return result;
        }
    }

    public static class VisualModules
    {
        public static readonly VisualModule<IMarkerVisualTheme> InfernoTheme = new VisualModule<IMarkerVisualTheme>("Inferno",1,b=>new Inferno(b));
        public static readonly VisualModule<IMarkerVisualTheme> NecroTheme = new VisualModule<IMarkerVisualTheme>("Necro",1,b=>new Necro(b));
        public static readonly VisualModule<IFireballRenderer> Ember = new VisualModule<IFireballRenderer>("Ember",1,b=>new EmberFireball(b));
        public static readonly VisualModule<IFireballRenderer> Rune = new VisualModule<IFireballRenderer>("Rune",1,b=>new RuneFireball(b));
    }
}
