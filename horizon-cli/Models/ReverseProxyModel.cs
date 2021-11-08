using System.ComponentModel.DataAnnotations;
using CommandDotNet;
using FluentValidation;
using FluentValidation.Attributes;
using FluentValidation.Validators;

namespace horizon_cli.Models
{
    [Validator(typeof(ReverseProxyModelValidator))]
    public class ReverseProxyModel : ClientModel
    {
        [Required, Operand(Name = "Portmap", Description = "Mapping of the ports. Follows this format: [local-port]:[reverse-proxy-port]")] 
        public string Portmap { get; set; }
    }
    
    public class ReverseProxyModelValidator : AbstractValidator<ReverseProxyModel>
    {
        public ReverseProxyModelValidator()
        {
            RuleFor(x => x).SetValidator(new ClientModelValidator());
            RuleFor(x => x.Portmap).NotEmpty().Custom(PortmapIsValid);
        }
        private void PortmapIsValid(string portmap, CustomContext ctx)
        {
            var portmapParts = portmap.Split(':');
            if (portmapParts.Length != 2)
            {
                ctx.AddFailure("The specified port map is not valid, the map follows this format: [local-port]:[reverse-proxy-port]");
                return;
            }

            if (!int.TryParse(portmapParts[0], out var v) || v is <= 0 or > 65536)
            {
                ctx.AddFailure($"The specified port \"{portmapParts[0]}\" in the map is not valid, the map follows this format: [local-port]:[reverse-proxy-port]");
                return;
            }

            if (!int.TryParse(portmapParts[1], out var x) || x is <= 0 or > 65536)
            {
                ctx.AddFailure($"The specified port \"{portmapParts[1]}\" in the map is not valid, the map follows this format: [local-port]:[reverse-proxy-port]");
            }
        }
    }
}