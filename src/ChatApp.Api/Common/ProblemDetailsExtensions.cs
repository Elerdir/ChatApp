namespace ChatApp.Api.Common;

public static class ProblemDetailsExtensions
{
    public static IResult WithProblemCode(this IResult result, string code)
    {
        return Results.Extensions.Problem(result, code);
    }

    private static class Results
    {
        public static class Extensions
        {
            public static IResult Problem(IResult inner, string code) => new Wrapper(inner, code);

            private sealed class Wrapper : IResult
            {
                private readonly IResult _inner;
                private readonly string _code;

                public Wrapper(IResult inner, string code) { _inner = inner; _code = code; }

                public async Task ExecuteAsync(HttpContext httpContext)
                {
                    // Hack-free varianta: jednodušší je to řešit ve vašem ProblemResults.ToHttp()
                    // Pokud už máte ToHttp() a Result pattern, dej mi ho a napojíme to čistě.
                    await _inner.ExecuteAsync(httpContext);
                }
            }
        }
    }
}