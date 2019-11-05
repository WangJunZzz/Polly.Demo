using System;
using System.Net;
using System.Net.Http;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using Polly.Timeout;

namespace Polly.Demo
{
    class Program
    {
        //http://beckjin.com/2019/09/07/polly-http-client/
        //http://www.vnfan.com/buffett/d/aabd7f14eee480f0.html
        //http://www.52sye.com/NewsInfo/1378356.html
        //https://blog.wayneshao.com/x%E5%BE%AE%E6%9C%8D%E5%8A%A1%E5%AD%A6%E4%B9%A0xPolly%EF%BC%9A%E7%86%94%E6%96%AD%E9%99%8D%E7%BA%A7%E7%BB%84%E4%BB%B6.html
        static void Main(string[] args)
        {
            //Case_Retry();
            //Case_Fallback();
            //Case_TimeOut();
            //Case_CircuitBreaker();
            //Case_AdvancedCircuitBreaker();
            //Case_Group();
            Case_Group_1();
            Console.ReadKey();
        }

        /// <summary>
        /// 重试
        /// </summary>
        private static void Case_Retry()
        {
            var client = new HttpClient();
            Policy
                // 处理什么异常，比如httprequrest异常
                .Handle<HttpRequestException>()
                // 或者处理response的httpstatuscode 不等于200的情况
                .OrResult<HttpResponseMessage>(res => res.StatusCode != HttpStatusCode.OK)
                // 重试次数 3,参数为此次异常、当前重试次数和当前执行的上下文
                .Retry(3,
                    (ex, retryCount, content) =>
                    {
                        Console.WriteLine(
                            $"请求Api异常,进行第{retryCount}次重试,ErrorCode:{ex.Result.StatusCode}");
                    })
                // 要执行的任务
                .Execute(() =>
                {
                    HttpResponseMessage res =
                        client.GetAsync("http://qa.fanyouvip.com/Social/policy/1").Result;
                    return res;
                });
        }

        /// <summary>
        /// 降级处理
        /// </summary>
        private static void Case_Fallback()
        {
            var client = new HttpClient();
            Policy
                .Handle<HttpRequestException>()
                .OrResult<HttpResponseMessage>(res => res.StatusCode != HttpStatusCode.OK)
                // 出现异常只会回退处理
                .Fallback(() =>
                {
                    HttpResponseMessage res =
                        client.GetAsync("http://qa.fanyouvip.com/Social/policy/2").Result;
                    Console.WriteLine("Fallback（降级）处理.");
                    return res;
                })
                .Execute(() =>
                {
                    HttpResponseMessage res =
                        client.GetAsync("http://qa.fanyouvip.com/Social/policy/1").Result;
                    return res;
                });
        }

        /// <summary>
        /// 超时处理
        /// </summary>
        private static void Case_TimeOut()
        {
            var timeoutPolicy = Policy.TimeoutAsync(1, TimeoutStrategy.Pessimistic,
                (context, timespan, task) =>
                {
                    Console.WriteLine("请求超时.");
                    return Task.CompletedTask;
                });
            timeoutPolicy.ExecuteAsync(async () =>
            {
                var client = new HttpClient();
                await client.GetAsync("http://localhost:5000/home/delay");
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// 短路保护，熔断器
        /// </summary>
        private static void Case_CircuitBreaker()
        {
            var client = new HttpClient();
            var circuitBreaker = Policy.Handle<Exception>()
                // 熔断前允许出现3次错误,熔断时间10s,熔断时触发, 熔断恢复时触发,在熔断时间到了之后触发
                .CircuitBreaker(3, TimeSpan.FromSeconds(10),
                    (ex, breakDelay) =>
                    {
                        //熔断时触发
                        Console.WriteLine("断路器打开,熔断触发.");
                    },
                    () =>
                    {
                        //熔断恢复时触发
                        Console.WriteLine("熔断器关闭了.");
                    },
                    () =>
                    {
                        //在熔断时间到了之后触发
                        Console.WriteLine("熔断时间到，进入半开状态");
                    });

            // 模拟多次调用，触发熔断
            for (int i = 1; i <= 150; i++)
            {
                try
                {
                    circuitBreaker.Execute(() =>
                    {
                        Console.WriteLine($"第{i}次开始执行.");
                        var res = client.GetAsync("http://localhost:5000/home/delay").Result;
                        Console.WriteLine($"第{i}次执行：正常:" + res.StatusCode);
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        return res;
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine($"第{i}次执行：异常:" + e.Message);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }

        /// <summary>
        /// 熔断高级配置,根据时间段内总请求数中的异常比例触发熔断
        /// </summary>
        private static void Case_AdvancedCircuitBreaker()
        {
            var client = new HttpClient();
            var advancedCircuitBreaker = Policy.Handle<Exception>()
                .AdvancedCircuitBreaker(0.5, TimeSpan.FromSeconds(10), 3, TimeSpan.FromSeconds(10),
                    (ex, breakDelay) =>
                    {
                        Console.WriteLine("断路器打开,熔断触发.");
                    }, () =>
                    {
                        Console.WriteLine("熔断器关闭了.");
                    }, () =>
                    {
                        Console.WriteLine("熔断时间到，进入半开状态");
                    });
            
            // 模拟多次调用，触发熔断
            for (int i = 1; i <= 150; i++)
            {
                try
                {
                    advancedCircuitBreaker.Execute(() =>
                    {
                        Console.WriteLine($"第{i}次开始执行.");
                        var res = client.GetAsync("http://localhost:5000/home/delay").Result;
                        Console.WriteLine($"第{i}次执行：正常:" + res.StatusCode);
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        return res;
                    });
                }
                catch (Exception e)
                {
                    Console.WriteLine($"第{i}次执行：异常:" + e.Message);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
        }

        /// <summary>
        /// 组合使用
        /// </summary>
        private static void Case_Group()
        {
            var client = new HttpClient();

            //定义一个超时策略

            var timeoutPolicy = Policy.TimeoutAsync(11, TimeoutStrategy.Pessimistic,
                (context, timespan, task) =>
                {
                    Console.WriteLine("请求超时.");
                    return Task.CompletedTask;
                });
            
            // 定义重试策略
            var retryPolicy = Policy.Handle<HttpRequestException>().Or<TimeoutException>()
                .Or<TimeoutRejectedException>()
                .WaitAndRetryAsync(
                    retryCount: 2,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    ,((exception, span, context) =>
                    {
                        Console.WriteLine("正在重试.");
                    }));
            
            // 定义回退策略
            var fallbackPolicy = Policy<object>.Handle<Exception>()
                .FallbackAsync(
                    fallbackValue: "data",
                    onFallbackAsync: (exception, context) =>
                    {
                        Console.WriteLine("进入回退策略.");
                        return Task.CompletedTask;
                    });

            // 组合使用
            var res = fallbackPolicy.WrapAsync(Policy.WrapAsync(retryPolicy, timeoutPolicy))
                .ExecuteAsync(
                    async () =>
                    {
                        Console.WriteLine("start");
                        return await client.GetAsync("http://localhost:5000/home/delay");
                    });
            Console.WriteLine(res);
        }

        private static void Case_Group_1()
        {
            //定义超时
            var timeOut = Policy.Timeout(TimeSpan.FromSeconds(10),
                ((context, timespan, task) =>
                {
                    Console.WriteLine("请求超时.");
                }));
            
            //定义重试
            var retry = Policy.Handle<Exception>()
                .Retry(3, ((exception, retryCount, context) =>
                {
                    Console.WriteLine($"第{retryCount}次重试.");
                }));

            // 定义熔断策略
            var circuitBreaker = Policy.Handle<Exception>()
                // 熔断前允许出现3次错误,熔断时间10s,熔断时触发, 熔断恢复时触发,在熔断时间到了之后触发
                .CircuitBreaker(3, TimeSpan.FromSeconds(10),
                    (ex, breakDelay) =>
                    {
                        //熔断时触发
                        Console.WriteLine("断路器打开,熔断触发.");
                    },
                    () =>
                    {
                        //熔断恢复时触发
                        Console.WriteLine("熔断器关闭了.");
                    },
                    () =>
                    {
                        //在熔断时间到了之后触发
                        Console.WriteLine("熔断时间到，进入半开状态");
                    });
            
            //定义回退策略
            var fallback = Policy.Handle<Exception>()
                .Fallback(() =>
                {
                    Console.WriteLine("正在降级处理.");
                });

            fallback.Wrap(Policy.Wrap(circuitBreaker,retry, timeOut)).Execute(() =>
            {
                Console.WriteLine("start.");
            });
        }
    }
}