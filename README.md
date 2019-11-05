### Polly
>在.Net Core中有一个被.Net基金会认可的库Polly，它一种弹性和瞬态故障处理库，可以用来简化对服务熔断降级的处理。
>Polly的策略主要由“故障”和“动作”两个部分组成，“故障”可以包括异常、超时等情况，“动作”则包括Fallback（降级）、重试（Retry）、熔断（Circuit-Breaker）等。策略则用来执行业务代码，当业务代码出现了“故障”中的情况时就开始执行“动作”。
主要包含以下功能：
+ 重试(Retry)
+ 断路器(Circuit-breaker)
+ 超时检测(Timeout)
+ 回退(FallBack)
+ 策略包装(PolicyWrap)

#### 故障定义
>故障也可以说是触发条件，它使用Handle<T>来定义，表示在什么情况下，才对其进行处理（熔断，降级，重试等）。
```csharp
// 单一异常种类
Policy.Handle<HttpRequestException>();
// 带条件判断的单一异常
Policy.Handle<SqlException>(ex => ex.Number == 10)
// 多种异常
Policy.Handle<HttpRequestException>().Or<OperationCanceledException>()
// 多种异常
Policy.Handle<HttpRequestException>().OrResult<HttpResponseMessage>(res => res.StatusCode != HttpStatusCode.OK)
// 返回结果异常
Policy.HandleResult<HttpResponseMessage>(r => r.StatusCode != HttpStatusCode.OK)
```

#### 重试(Retry)
>重试就是指Polly在调用失败时捕获我们指定的异常，并重新发起调用，如果重试成功，那么对于调用者来说，就像没有发生过异常一样。在网络调用中经常出现瞬时故障，那么重试机制就非常重要。
```csharp
var client = new HttpClient();
Policy
    // 处理什么异常，比如httprequrest异常
    .Handle<HttpRequestException>()
    // 或者处理response的httpstatuscode 不等于200的情况
    .OrResult<HttpResponseMessage>(res => res.StatusCode != HttpStatusCode.OK)
    // 重试次数 3
    .Retry(3,
        (ex, retryCount,content) => 
        {
            Console.WriteLine($"请求Api异常,进行第{retryCount}次重试,ErrorCode:{ex.Result.StatusCode}");
        })
    // 要执行的任务
    .Execute(() =>
    {
        HttpResponseMessage res = client.GetAsync("http://qa.xx.com/Social/policy/1").Result;
        return res;
    });
```

#### 回退(FallBack)
>回退也称服务降级，用来指定发生故障时的备用方案。
``` csharp
var client = new HttpClient();
Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(res => res.StatusCode != HttpStatusCode.OK)
    // 出现异常只会回退处理
    .Fallback(() =>
    {
        HttpResponseMessage res =
            client.GetAsync("http://qa.xx.com/Social/policy/2").Result;
        Console.WriteLine("Fallback（降级）处理.");
        return res;
    })
    
    .Execute(() =>
    {
        HttpResponseMessage res =
            client.GetAsync("http://qa.xx.com/Social/policy/1").Result;
        return res;
    });
```
#### 超时(Timeou)
Polly支持两种超时策略：
+ TimeoutStrategy.Pessimistic： 悲观模式
当委托到达指定时间没有返回时，不继续等待委托完成，并抛超时TimeoutRejectedException异常。
+ TimeoutStrategy.Optimistic：乐观模式
这个模式依赖于 [co-operative cancellation](https://docs.microsoft.com/zh-cn/dotnet/standard/threading/cancellation-in-managed-threads?redirectedfrom=MSDN)，只是触发CancellationTokenSource.Cancel函数，需要等待委托自行终止操作。
```bash
var timeoutPolicy = Policy.TimeoutAsync(1, TimeoutStrategy.Pessimistic,
    (context, timespan, task) =>
    {
        Console.WriteLine("请求超时.");
        return Task.CompletedTask;
    });
timeoutPolicy.ExecuteAsync(async () =>
{
    var client = new HttpClient();
    await   client.GetAsync("http://localhost:5000/home/delay");
    return Task.CompletedTask;
});
```
#### 熔断(Circuit-breaker)
>如果调用某个目标服务出现过多超时、异常等情况，可以采取一定时间内熔断该服务的调用，熔断期间的请求将不再继续调用目标服务，而是直接返回，节约资源，提高服务的稳定性，熔断周期结束后如果目标服务情况好转则恢复调用。
##### 熔断状态
+ 打开（Open）
>熔断器打开状态，此时对目标服务的调用都直接返回错误，熔断周期内不会走网络请求，当熔断周期结束时进入半开状态；
+ 关闭（Closed）
>关闭状态下正常发生网络请求，但会记录符合熔断条件的连续执行次数，如果错误数量达到设定的阈值（如果在没有达到阈值之前恢复正常，之前的累积次数将会归零），熔断状态进入到打开状态；
+ 半开（Half-Open）
>半开状态下允许定量的服务请求，如果调用都成功则认为恢复了，关闭熔断器，否则认为还没好，又回到熔断器打开状态；

**注意：为了服务的稳定性，在执行需要多次 Retry（重试策略）的情况下，最好组合熔断策略，预防可能存在的风险。**
```csharp
var client = new HttpClient();
var ciruitBreaker = Policy.Handle<Exception>()
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
        }
// 模拟多次调用，触发熔断
for (int i = 1; i <= 150; i++)
{
    try
    {
        ciruitBreaker.Execute(() =>
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
```

##### 熔断高级配置
>根据时间段内总请求数中的异常比例触发熔断
```csharp
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
```

#### 策略包装(PolicyWrap)
>策略包提供了一种灵活的方式来封装多个弹性策略(从右往左).
```csharp
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
    })
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
    }
fallback.Wrap(Policy.Wrap(circuitBreaker,retry, timeOut)).Execute(() =>
{
    Console.WriteLine("start.");
});
```


### HttpClientFactory
+ 提供一个中心位置，用于命名和配置逻辑 HttpClient 对象.
+ 管理 HttpClientMessageHandlers 的生存期，避免在HttpClient 生存期时出现问题.
+ 在 HttpClient 中委托处理程序并实现基于 Polly 的中间件以利用 Polly 的复原策略。
https://docs.microsoft.com/zh-cn/aspnet/core/fundamentals/http-requests?view=aspnetcore-3.0
#### 简单使用
> Install Microsoft.Extensions.Http
> 如果有多个可以同时使用
```csharp
// StartUp->ConfigureServices
services.AddHttpClient("local",options =>
{
    options.BaseAddress = new Uri("http://localhost:5000");
}
services.AddHttpClient("fanyou",options =>
{
    options.BaseAddress = new Uri("http://qa.fanyouvip.com");
});

//使用
[Route("client")]
public class ClientController : ControllerBase
{
    private readonly IHttpClientFactory _clientFactory;
    public ClientController(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    [HttpGet("Local")]
    public async Task<IActionResult> Local()
    {
        var client = _clientFactory.CreateClient("local");
        var res = await client.GetAsync("/home/delay");
        return Ok(res);
    }
    
    [HttpGet("Fanyou")]
    public async Task<IActionResult> Fanyou()
    {
        var client = _clientFactory.CreateClient("fanyou");
        var res = await client.GetAsync("/social");
        return Ok(res);
    }
}
```

#### 结合Polly
> Install Microsoft.Extensions.Http.Polly
```csharp
// 第一种方式
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

//第二种方式
services.AddHttpClient("Test",
        options => { options.BaseAddress = new Uri("http://localhost:5003"); })
    .AddPolicyHandler(RetryPolicy())
    .AddPolicyHandler(CircuiBreakerPolicy());

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
```
