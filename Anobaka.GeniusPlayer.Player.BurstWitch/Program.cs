using Anobaka.GeniusPlayer.Player.BurstWitch.Cli;
using Anobaka.GeniusPlayer.Player.BurstWitch.Modules;
using Bootstrap.Extensions;
using CommandLine;

try
{
    Parser.Default.ParseArguments<ListOptions, UpgradeOptions>(args)
        .MapResult(
            (ListOptions o) =>
            {
                new RuneManagement(o).Upgrade().ConfigureAwait(false).GetAwaiter().GetResult();
                return 0;
            },
            (UpgradeOptions o) =>
            {
                new RuneManagement(o).Upgrade().ConfigureAwait(false).GetAwaiter().GetResult();
                return 0;
            },
            errs =>
            {
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
                return 1;
            }
        );
}
catch (Exception e)
{
    Console.WriteLine(e.BuildFullInformationText());
    Console.WriteLine("Press enter to exit.");
    Console.ReadLine();
}