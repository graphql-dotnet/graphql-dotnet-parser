using System;
using PublicApiGenerator;
using Shouldly;
using Xunit;

namespace GraphQLParser.ApiTests
{
    public class ApiApprovalTests
    {
        [Theory]
        [InlineData(typeof(Lexer))]
        public void Public_Api_Should_Not_Change_Inadvertently(Type type)
        {
            string publicApi = type.Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                IncludeAssemblyAttributes = false
            });

            publicApi.ShouldMatchApproved(options => options.WithFilenameGenerator((testMethodInfo, discriminator, fileType, fileExtension) => $"{type.Assembly.GetName().Name}.{fileType}.{fileExtension}"));
        }
    }
}
