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
        [Required, Operand(Name = "Portmap", Description = "Mapping of the ports. Follows this format: [inbound-port]:[outbound-address]:[outbound-port]")] 
        public string Portmap { get; set; }
    }

    public class ProxyModelValidator : AbstractValidator<ProxyModel>
    {
        public ProxyModelValidator()
        {
            RuleFor(x => x).SetValidator(new ClientModelValidator());
            RuleFor(p => p.Portmap).Custom(PortmapIsValid);
        }

        public static void PortmapIsValid(string portmap, CustomContext ctx)
        {
            if (string.IsNullOrEmpty(portmap))
            {
                ctx.AddFailure("The specified port map cannot be empty");
                return;
            }
            if (!Extensions.ParseMap(portmap).HasValue){
                ctx.AddFailure("The specified port map is not valid, the map follows this format: [inbound-port]:[outbound-address]:[outbound-port]");
            }
        }
    }
}