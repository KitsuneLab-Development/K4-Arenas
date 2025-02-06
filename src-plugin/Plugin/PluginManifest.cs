namespace K4Arenas
{
    using CounterStrikeSharp.API.Core;

    public sealed partial class Plugin : BasePlugin
    {
        public override string ModuleName => "K4-Arenas";

        public override string ModuleDescription => "An arena plugin for Counter-Strike2";

        public override string ModuleAuthor => "K4ryuu";

        public override string ModuleVersion => "2.0.1 " +
#if RELEASE
            "(release)";
#else
            "(debug)";
#endif
    }
}