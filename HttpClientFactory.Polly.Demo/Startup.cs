using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace HttpClientFactory.Polly.Demo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient("fanyou",
                options => { options.BaseAddress = new Uri("http://qa.fanyouvip.com"); });

            // 添加添加Polly,   Install Microsoft.Extensions.Http.Polly
            services.AddHttpClient("local",
                    options => { options.BaseAddress = new Uri("http://localhost:5000"); })
                .AddTransientHttpErrorPolicy(p =>
                {
                    var handlers = p.OrResult(result => result.StatusCode != HttpStatusCode.OK)
                        .RetryAsync(3,
                            (ex, retryCount, context) =>
                            {
                                Console.WriteLine($"第{retryCount}次重试.异常:{ex.Exception.Message}");
                            });
                    return handlers;
                }).AddTransientHttpErrorPolicy(p =>
                {
                    var breaker = p.CircuitBreakerAsync(3, TimeSpan.FromSeconds(10));
                    return breaker;
                });

            services.AddHttpClient("Test",
                    options => { options.BaseAddress = new Uri("http://localhost:5003"); })
                .AddPolicyHandler(RetryPolicy())
                .AddPolicyHandler(CircuiBreakerPolicy());
            
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }

        /// <summary>
        /// 重试策略
        /// </summary>
        /// <returns>IAsyncPolicy<HttpResponseMessage></returns>
        private IAsyncPolicy<HttpResponseMessage> RetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(res => res.StatusCode != HttpStatusCode.OK)
                .WaitAndRetryAsync(3, retryCount => TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
        }

        /// <summary>
        /// 熔断策略
        /// </summary>
        /// <returns>IAsyncPolicy<HttpResponseMessage></returns>
        private IAsyncPolicy<HttpResponseMessage> CircuiBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));
        }
    }
}