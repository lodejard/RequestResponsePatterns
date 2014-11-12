using System;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using RequestResponsePatterns;
using System.Threading.Tasks;

namespace TestApp
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app)
        {
            app.UseMiddleware<WTFBIGYAcceptEncodingMiddleware>();
            app.UseMiddleware<ChunkedMiddleware>();
            app.UseMiddleware<ResponseBufferMiddleware>();
            app.UseMiddleware<GZipMiddleware>();

            app.Map("/wtfbigy", map =>
            {
                map.Use(EnsureWTFBIGY);
                map.Run(PageOfText);
            });

            app.Map("/restart", map =>
            {
                map.Use(RestartErrorPage);
                map.Run(PageOfText);
            });

            app.Map("/norestart", map =>
            {
                map.Use(RestartErrorPage);
                map.Use(FlushAndAddMoreText);
                map.Run(PageOfText);
            });

            app.Map("/flush", map =>
            {
                map.Use(FlushAndAddMoreText);
                map.Run(PageOfText);
            });

            app.Run(PageOfText);
        }

        async Task PageOfText(HttpContext context)
        {
            context.Response.ContentType = "text/plain";
            for(int index = 0; index != 500; ++index)
            {
                await context.Response.WriteAsync("This is text for line " + index + "\r\n");
            }
        }

        RequestDelegate EnsureWTFBIGY(RequestDelegate next)
        {
            return ctx =>
            {
                ctx.Response.EnsureWTFBIGY();
                return next(ctx);
            };
        }

        RequestDelegate RestartErrorPage(RequestDelegate next)
        {
            return async ctx =>
            {
                await next(ctx);
                if (ctx.Response.CanRestart())
                {
                    ctx.Response.Restart();
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.WriteAsync("This is an error page\r\n");
                }
                else
                {
                    await ctx.Response.WriteAsync("This is an added-on error page\r\n");
                }
            };
        }

        RequestDelegate FlushAndAddMoreText(RequestDelegate next)
        {
            return async ctx =>
            {
                await next(ctx);
                await ctx.Response.Body.FlushAsync();
                await ctx.Response.WriteAsync("This is more text after flushing\r\n");
                await ctx.Response.WriteAsync("And slightly more text after flushing\r\n");
            };
        }
    }
}
