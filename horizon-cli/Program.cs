using System.Threading.Tasks;
using CommandDotNet;
using CommandDotNet.FluentValidation;

namespace horizon_cli
{
    class Program
    {
        public static Task Main(string[] args) => 
            new AppRunner<HorizonCLI>()
                .UseTypoSuggestions()
                .UseCancellationHandlers()
                .UseResponseFiles()
                .UseFluentValidation(showHelpOnError: true)
                .RunAsync(args);
    }
}
