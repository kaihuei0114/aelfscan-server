using HotChocolate.Types;

namespace AElfScanServer.Common.BFF.Core.Adaptor;

public class FromJsonDirectiveType : DirectiveType<FromJsonDirective>
{
    protected override void Configure(IDirectiveTypeDescriptor<FromJsonDirective> descriptor)
    {
        descriptor.Name(DirectiveConstant.CustomJsonDirectiveName);
        descriptor.Location(DirectiveLocation.Object | DirectiveLocation.FieldDefinition);
        descriptor.Use<FromJsonDirectiveMiddleware>();
    }
}