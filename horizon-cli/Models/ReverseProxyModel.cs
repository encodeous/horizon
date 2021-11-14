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
        [Required, Operand(Name = "Portmap", Description = "Mapping of the ports. Follows this format: [inbound-port]:[outbound-address]:[outbound-port]")] 
        public string Portmap { get; set; }
    }
    
    public class ReverseProxyModelValidator : AbstractValidator<ReverseProxyModel>
    {
        public ReverseProxyModelValidator()
        {
            RuleFor(x => x).SetValidator(new ClientModelValidator());
            RuleFor(x => x.Portmap).Custom(ProxyModelValidator.PortmapIsValid);
        }
    }
}