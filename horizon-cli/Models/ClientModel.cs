using System;
using System.ComponentModel.DataAnnotations;
using CommandDotNet;
using FluentValidation;
using FluentValidation.Attributes;
using horizon;
using Microsoft.Extensions.Logging;

namespace horizon_cli.Models
{
    public class ClientModel : IArgumentModel
    {
        [Required, Operand(Name = "Address", Description = "Address of the Horizon server, ex. example.com:1234")]
        public string ServerAddress { get; set; }

        [Option(LongName = "token", ShortName = "t",
            Description = "The token used to authenticate & encrypt the connection with the server")]
        public string Token { get; set; } = "default";

        [Option(LongName = "high-performance", ShortName = "p",
            Description = "Disables encryption and the head-of-line blocking algorithm")]
        public bool HighPerformance  { get; set; } = false;
        
        [Option(LongName = "log-level", ShortName = "l",
            Description = "Configures the logging level.")]
        public LogLevel LoggingLevel { get; set; } = LogLevel.Information;
        
        [Option(LongName = "Use Https transport", ShortName = "s",
            Description = "Allows the client to connect to a Horizon server behind a ssl server or proxy.")]
        public bool SecureWebsockets  { get; set; } = false;
    }
    public class ClientModelValidator : AbstractValidator<ClientModel>
    {
        public ClientModelValidator()
        {
            RuleFor(x => x.ServerAddress).Must(x =>
            {
                if (string.IsNullOrEmpty(x)) return false;
                if (Uri.TryCreate("ws://" + x, UriKind.Absolute, out var p))
                {
                    if (p.Scheme != "wss" && p.Scheme != "ws")
                    {
                        return false;
                    }
                    return true;
                }
                return false;
            }).WithMessage("A valid server address is required. Do not include any uri schemes.");
            RuleFor(x => x.Token).NotEmpty()
                .WithMessage("The token must not be empty");
        }
    }
}