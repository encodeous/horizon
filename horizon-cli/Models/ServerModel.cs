using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using CommandDotNet;
using FluentValidation;
using FluentValidation.Attributes;
using FluentValidation.Validators;
using ValidationContext = FluentValidation.ValidationContext;
using ValidationException = FluentValidation.ValidationException;

namespace horizon_cli.Models
{
    [Validator(typeof(ServerModelValidator))]
    public class ServerModel : IArgumentModel
    {
        [Required, Operand(Name = "Bind", Description = "The address that Horizon will listen to, ex. 0.0.0.0:4000")]
        public string Bind { get; set; }

        [Option(LongName = "reverse-bind", ShortName = "b",
            Description = "Specify zero or more ports that Horizon will listen to for reverse proxy connections")]
        public IEnumerable<string> ReverseBinds { get; set; } = new List<string>();
        
        [Option(LongName = "token", ShortName = "t",
            Description = "The token used to authenticate & encrypt the connection with the client")]
        public string Token { get; set; } = "default";
        
        [Option(LongName = "whitelist", ShortName = "w",
            Description = "Enables the whitelist filter (blacklist by default)")]
        public bool Whitelist  { get; set; } = false;

        [Option(LongName = "filter", ShortName = "f",
            Description =
                "Allows/disallows (depending on black/whitelist) certain proxy destinations, specify zero or more patterns." +
                " Format: [host-name-regex]:[port-range-start]:[port-range-end]")]
        public IEnumerable<string> RemotesFilter { get; set; } = new List<string>();
    }

    public class ServerModelValidator : AbstractValidator<ServerModel>
    {
        public ServerModelValidator()
        {
            RuleFor(x => x.Bind).NotEmpty().Custom((str,y) =>
            {
                if (str.Count(x => x == ':') < 1)
                {
                    y.AddFailure("Expected one or more colon in the bind format.");
                    return;
                }
                int idx = str.LastIndexOf(":", StringComparison.Ordinal);
                if (!IPAddress.TryParse(str[..idx], out _))
                {
                    y.AddFailure($"The address {str[..idx]} is invalid.");
                    return;
                }
                if (!int.TryParse(str[(idx + 1)..], out var port) || port is <= 0 or > 65535)
                {
                    y.AddFailure($"The port {str[(idx + 1)..]} is invalid.");
                    return;
                }
                return;
            });
            
            RuleFor(x => x.Token).NotEmpty()
                .WithMessage("The token must not be empty");
            
            RuleFor(x => x.ReverseBinds).Must(x =>
            {
                return x.All(v=> int.TryParse(v, out var k) && k is <= 65535 and > 0);
            }).WithMessage("Please double check the reverse proxy ports.");

            RuleFor(x => x.RemotesFilter)
                .Custom((x, y) => x.All(v => ValidateFilter(v, y)));
        }

        public bool ValidateFilter(string str, CustomContext ctx)
        {
            if(str.Count(x => x == ':') < 2)
                return false;

            var id1 = str.LastIndexOf(":", StringComparison.Ordinal);
            var id2 = str[..id1].LastIndexOf(":", StringComparison.Ordinal);
            string s1 = str[..id2], s2 = str[(id2+1)..id1], s3 = str[(id1+1)..];
            if (!IsValidRegex(s1))
            {
                ctx.AddFailure($"The specified regex {s1} is not valid");
                return false;
            }
            if(!int.TryParse(s2, out var port) || port is <= 0 or > 65535)
            {
                ctx.AddFailure($"The port {s2} is not valid!");
                return false;
            }
            if(!int.TryParse(s3, out var port2) || port2 is <= 0 or > 65535)
            {
                ctx.AddFailure($"The port {s3} is not valid!");
                return false;
            }
            return true;
        }
        
        private static bool IsValidRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;

            try
            {
                Regex.Match("", pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }
    }
}