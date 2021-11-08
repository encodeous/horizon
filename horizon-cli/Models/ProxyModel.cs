using System;
using System.ComponentModel.DataAnnotations;
using CommandDotNet;
using FluentValidation;
using FluentValidation.Attributes;
using FluentValidation.Validators;

namespace horizon_cli.Models
{
    [Validator(typeof(ProxyModelValidator))]
    public class ProxyModel : ClientModel
    {
        [Required, Operand(Name = "Portmap", Description = "Mapping of the ports. Follows this format: [local-port]:[proxied-address]:[proxied-port]")] 
        public string Portmap { get; set; }
    }

    public class ProxyModelValidator : AbstractValidator<ProxyModel>
    {
        public ProxyModelValidator()
        {
            RuleFor(x => x).SetValidator(new ClientModelValidator());
            RuleFor(p => p.Portmap).NotEmpty().Custom(PortmapIsValid);
        }

        private void PortmapIsValid(string portmap, CustomContext ctx)
        {
            var portmapParts = portmap.Split(':');
            if (portmapParts.Length != 3)
            {
                ctx.AddFailure("The specified port map is not valid, the map follows this format: [local-port]:[proxied-address]:[proxied-port]");
                return;
            }

            if (!int.TryParse(portmapParts[0], out var v) || v is <= 0 or > 65536)
            {
                ctx.AddFailure($"The specified port \"{portmapParts[0]}\" in the map is not valid, the map follows this format: [local-port]:[proxied-address]:[proxied-port]");
                return;
            }

            if (!int.TryParse(portmapParts[2], out var x) || x is <= 0 or > 65536)
            {
                ctx.AddFailure($"The specified port \"{portmapParts[2]}\" in the map is not valid, the map follows this format: [local-port]:[proxied-address]:[proxied-port]");
            }
        }
    }
}