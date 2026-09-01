namespace FeatureCli;

internal sealed record PropertySpec(string Name, string Type)
{
    public bool IsNonNullableString => Type == "string";
}

internal static class FeatureTemplates
{
    public static (string FileName, string Content)[] Query(
        string name, string feature, string entity, string rootNamespace, string dbContextNamespace, string dbContextType) =>
        new (string, string)[]
        {
            ($"{name}Query.cs", $$"""
            using {{dbContextNamespace}};
            using Domain.Common;
            using MediatR;

            namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{name}};

            // TODO: add query parameters, e.g. public record {{name}}Query(int Id) : IRequest<Result<{{name}}Response>>;
            public record {{name}}Query : IRequest<Result<{{name}}Response>>;

            public class {{name}}QueryHandler({{dbContextType}} context) : IRequestHandler<{{name}}Query, Result<{{name}}Response>>
            {
                public Task<Result<{{name}}Response>> Handle({{name}}Query request, CancellationToken cancellationToken)
                {
                    // TODO: query context.{{feature}} (Domain.Entities.{{entity}}) and map the result to {{name}}Response
                    throw new NotImplementedException();
                }
            }

            """),
            ($"{name}Response.cs", $$"""
            namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{name}};

            public class {{name}}Response
            {
                // TODO: define the properties returned to the caller
            }

            """)
        };

    public static (string FileName, string Content)[] Command(
        string name, string feature, string entity, string rootNamespace, string dbContextNamespace, string dbContextType) =>
        new (string, string)[]
        {
            ($"{name}Command.cs", $$"""
            using {{dbContextNamespace}};
            using Domain.Common;
            using MediatR;

            namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{name}};

            // TODO: add command parameters, e.g. public record {{name}}Command(int Id, string Title) : IRequest<Result>;
            public record {{name}}Command : IRequest<Result>;

            public class {{name}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{name}}Command, Result>
            {
                public Task<Result> Handle({{name}}Command request, CancellationToken cancellationToken)
                {
                    // TODO: implement using context.{{feature}} (Domain.Entities.{{entity}}) and context.SaveChangesAsync(cancellationToken)
                    throw new NotImplementedException();
                }
            }

            """),
            ($"{name}CommandValidator.cs", $$"""
            using FluentValidation;

            namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{name}};

            public class {{name}}CommandValidator : AbstractValidator<{{name}}Command>
            {
                public {{name}}CommandValidator()
                {
                    // TODO: add validation rules, e.g. RuleFor(v => v.Title).NotEmpty();
                }
            }

            """)
        };

    public static (string Category, string Name, (string FileName, string Content)[] Files)[] Crud(
        string entity, string plural, string feature, string rootNamespace, string dbContextNamespace,
        string dbContextType, string keyType, string entityNamespace, PropertySpec[] properties)
    {
        var createName = $"Create{entity}";
        var updateName = $"Update{entity}";
        var deleteName = $"Delete{entity}";
        var getByIdName = $"Get{entity}ById";
        var getAllName = $"Get{plural}";

        var createParams = string.Join(", ", properties.Select(p => $"{p.Type} {p.Name}"));
        var updateParams = properties.Length == 0
            ? $"{keyType} Id"
            : $"{keyType} Id, {string.Join(", ", properties.Select(p => $"{p.Type} {p.Name}"))}";

        // Fully-qualified: if --feature matches --entity (e.g. "Vessel"/"Vessel"), the generated namespace
        // (Features.Vessel.Commands.CreateVessel) has "Vessel" as a namespace segment, which shadows a bare
        // "Vessel" type reference - enclosing namespace members always win over `using`-imported types in C#.
        var createObjectInit = properties.Length == 0
            ? $"new {entityNamespace}.{entity}()"
            : $"new {entityNamespace}.{entity}\n        {{\n" +
              string.Join(",\n", properties.Select(p => $"            {p.Name} = request.{p.Name}")) +
              "\n        }";

        var updateAssignments = properties.Length == 0
            ? "// TODO: nothing to update yet - add properties with --properties"
            : string.Join("\n        ", properties.Select(p => $"entity.{p.Name} = request.{p.Name};"));

        var createRules = BuildCreateRules(properties);
        var updateRules = BuildUpdateRules(keyType, properties);

        var orderBy = properties.Length > 0
            ? $"\n            .OrderBy(x => x.{properties[0].Name})"
            : "\n            .OrderBy(x => x.Id)";

        var dtoProps = properties.Length == 0
            ? ""
            : "\n" + string.Join("\n\n", properties.Select(p =>
                $"    public {p.Type} {p.Name} {{ get; init; }}{(p.IsNonNullableString ? " = string.Empty;" : "")}"));

        return
        [
            ("Commands", createName,
            [
                ($"{createName}Command.cs", $$"""
                using {{dbContextNamespace}};
                using {{entityNamespace}};
                using Domain.Common;
                using MediatR;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{createName}};

                public record {{createName}}Command({{createParams}}) : IRequest<Result<{{keyType}}>>;

                public class {{createName}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{createName}}Command, Result<{{keyType}}>>
                {
                    public async Task<Result<{{keyType}}>> Handle({{createName}}Command request, CancellationToken cancellationToken)
                    {
                        var entity = {{createObjectInit}};

                        context.{{plural}}.Add(entity);

                        await context.SaveChangesAsync(cancellationToken);

                        return entity.Id;
                    }
                }

                """),
                ($"{createName}CommandValidator.cs", $$"""
                using FluentValidation;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{createName}};

                public class {{createName}}CommandValidator : AbstractValidator<{{createName}}Command>
                {
                    public {{createName}}CommandValidator()
                    {
                        {{createRules}}
                    }
                }

                """)
            ]),
            ("Commands", updateName,
            [
                ($"{updateName}Command.cs", $$"""
                using {{dbContextNamespace}};
                using Domain.Common;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{updateName}};

                public record {{updateName}}Command({{updateParams}}) : IRequest<Result>;

                public class {{updateName}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{updateName}}Command, Result>
                {
                    public async Task<Result> Handle({{updateName}}Command request, CancellationToken cancellationToken)
                    {
                        var entity = await context.{{plural}}
                            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

                        if (entity is null)
                        {
                            return Result.Failure(Error.EntityNotFound(nameof({{entityNamespace}}.{{entity}}), request.Id));
                        }

                        {{updateAssignments}}

                        await context.SaveChangesAsync(cancellationToken);

                        return Result.Success();
                    }
                }

                """),
                ($"{updateName}CommandValidator.cs", $$"""
                using FluentValidation;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{updateName}};

                public class {{updateName}}CommandValidator : AbstractValidator<{{updateName}}Command>
                {
                    public {{updateName}}CommandValidator()
                    {
                        {{updateRules}}
                    }
                }

                """)
            ]),
            ("Commands", deleteName,
            [
                ($"{deleteName}Command.cs", $$"""
                using {{dbContextNamespace}};
                using Domain.Common;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{deleteName}};

                public record {{deleteName}}Command({{keyType}} Id) : IRequest<Result>;

                public class {{deleteName}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{deleteName}}Command, Result>
                {
                    public async Task<Result> Handle({{deleteName}}Command request, CancellationToken cancellationToken)
                    {
                        var entity = await context.{{plural}}
                            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

                        if (entity is null)
                        {
                            return Result.Failure(Error.EntityNotFound(nameof({{entityNamespace}}.{{entity}}), request.Id));
                        }

                        context.{{plural}}.Remove(entity);

                        await context.SaveChangesAsync(cancellationToken);

                        return Result.Success();
                    }
                }

                """)
            ]),
            ("Queries", getByIdName,
            [
                ($"{getByIdName}Query.cs", $$"""
                using {{rootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};
                using {{dbContextNamespace}};
                using AutoMapper;
                using AutoMapper.QueryableExtensions;
                using Domain.Common;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{getByIdName}};

                public record {{getByIdName}}Query({{keyType}} Id) : IRequest<Result<{{entity}}Dto>>;

                public class {{getByIdName}}QueryHandler({{dbContextType}} context, IConfigurationProvider mapperConfiguration)
                    : IRequestHandler<{{getByIdName}}Query, Result<{{entity}}Dto>>
                {
                    public async Task<Result<{{entity}}Dto>> Handle({{getByIdName}}Query request, CancellationToken cancellationToken)
                    {
                        var result = await context.{{plural}}
                            .Where(x => x.Id == request.Id)
                            .ProjectTo<{{entity}}Dto>(mapperConfiguration)
                            .FirstOrDefaultAsync(cancellationToken);

                        return result is null
                            ? Result.Failure<{{entity}}Dto>(Error.EntityNotFound(nameof({{entityNamespace}}.{{entity}}), request.Id))
                            : result;
                    }
                }

                """)
            ]),
            ("Queries", getAllName,
            [
                ($"{getAllName}Query.cs", $$"""
                using {{dbContextNamespace}};
                using AutoMapper;
                using AutoMapper.QueryableExtensions;
                using Domain.Common;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};

                public record {{getAllName}}Query : IRequest<Result<List<{{entity}}Dto>>>;

                public class {{getAllName}}QueryHandler({{dbContextType}} context, IConfigurationProvider mapperConfiguration)
                    : IRequestHandler<{{getAllName}}Query, Result<List<{{entity}}Dto>>>
                {
                    public async Task<Result<List<{{entity}}Dto>>> Handle({{getAllName}}Query request, CancellationToken cancellationToken)
                    {
                        var result = await context.{{plural}}{{orderBy}}
                            .ProjectTo<{{entity}}Dto>(mapperConfiguration)
                            .ToListAsync(cancellationToken);

                        return result;
                    }
                }

                """),
                ($"{entity}Dto.cs", $$"""
                using AutoMapper;

                namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};

                public class {{entity}}Dto
                {
                    public {{keyType}} Id { get; init; }
                {{dtoProps}}

                    private class Mapping : Profile
                    {
                        public Mapping()
                        {
                            CreateMap<{{entityNamespace}}.{{entity}}, {{entity}}Dto>();
                        }
                    }
                }

                """)
            ])
        ];
    }

    public static (string FileName, string Content) Controller(
        string entity, string plural, string feature, string apiRootNamespace, string applicationRootNamespace, string keyType)
    {
        var createName = $"Create{entity}";
        var updateName = $"Update{entity}";
        var deleteName = $"Delete{entity}";
        var getByIdName = $"Get{entity}ById";
        var getAllName = $"Get{plural}";
        var getOneName = $"Get{entity}";

        var idRoute = keyType switch
        {
            "int" => "{id:int}",
            "long" => "{id:long}",
            "Guid" => "{id:guid}",
            _ => "{id}"
        };

        var fileName = $"{plural}Controller.cs";
        var content = $$"""
        using {{applicationRootNamespace}}.Features.{{feature}}.Commands.{{createName}};
        using {{applicationRootNamespace}}.Features.{{feature}}.Commands.{{deleteName}};
        using {{applicationRootNamespace}}.Features.{{feature}}.Commands.{{updateName}};
        using {{applicationRootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};
        using {{applicationRootNamespace}}.Features.{{feature}}.Queries.{{getByIdName}};
        using Microsoft.AspNetCore.Mvc;

        namespace {{apiRootNamespace}}.Controllers;

        public class {{plural}}Controller : BaseController
        {
            [HttpGet]
            public async Task<ActionResult<List<{{entity}}Dto>>> {{getAllName}}(CancellationToken cancellationToken)
            {
                var result = await this.Mediator.Send(new {{getAllName}}Query(), cancellationToken);
                return HandleResult(result);
            }

            [HttpGet("{{idRoute}}")]
            public async Task<ActionResult<{{entity}}Dto>> {{getOneName}}({{keyType}} id, CancellationToken cancellationToken)
            {
                var result = await this.Mediator.Send(new {{getByIdName}}Query(id), cancellationToken);
                return HandleResult(result);
            }

            [HttpPost]
            public async Task<ActionResult<{{keyType}}>> {{createName}}({{createName}}Command command, CancellationToken cancellationToken)
            {
                var result = await this.Mediator.Send(command, cancellationToken);
                return HandleCreatedResult(result, nameof({{getOneName}}), id => new { id });
            }

            [HttpPut("{{idRoute}}")]
            public async Task<IActionResult> {{updateName}}({{keyType}} id, {{updateName}}Command command, CancellationToken cancellationToken)
            {
                if (id != command.Id)
                {
                    return BadRequest();
                }

                var result = await this.Mediator.Send(command, cancellationToken);
                return HandleResult(result);
            }

            [HttpDelete("{{idRoute}}")]
            public async Task<IActionResult> {{deleteName}}({{keyType}} id, CancellationToken cancellationToken)
            {
                var result = await this.Mediator.Send(new {{deleteName}}Command(id), cancellationToken);
                return HandleResult(result);
            }
        }

        """;

        return (fileName, content);
    }

    private static string BuildCreateRules(PropertySpec[] properties)
    {
        var stringProps = properties.Where(p => p.IsNonNullableString).ToArray();

        if (stringProps.Length == 0)
        {
            return "// TODO: add validation rules, e.g. RuleFor(v => v.Title).NotEmpty();";
        }

        return string.Join("\n\n        ", stringProps.Select(p => $$"""
        RuleFor(v => v.{{p.Name}})
                    .NotEmpty()
                    .MaximumLength(200);
        """.Trim()));
    }

    private static string BuildUpdateRules(string keyType, PropertySpec[] properties)
    {
        var rules = new System.Collections.Generic.List<string>();

        if (keyType == "int")
        {
            rules.Add("RuleFor(v => v.Id)\n                    .GreaterThan(0);");
        }

        rules.AddRange(properties.Where(p => p.IsNonNullableString)
            .Select(p => $"RuleFor(v => v.{p.Name})\n                    .NotEmpty()\n                    .MaximumLength(200);"));

        return rules.Count == 0
            ? "// TODO: add validation rules"
            : string.Join("\n\n        ", rules);
    }
}
