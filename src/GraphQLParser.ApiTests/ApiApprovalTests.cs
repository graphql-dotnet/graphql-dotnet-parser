using System;
using PublicApiGenerator;
using Shouldly;
using Xunit;

namespace GraphQLParser.ApiTests
{
    public class ApiApprovalTests
    {
        [Fact]
        public void Public_Api_Should_Not_Change_Inadvertently()
        {
            var api = typeof(Lexer).Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                IncludeAssemblyAttributes = false
            });

            Console.WriteLine(api);

            api.ShouldMatchApproved();
        }
    }
}
