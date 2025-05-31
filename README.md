# EBus

**EBus** is a lightweight mediator library for .NET that provides in-process messaging for commands/queries (CQRS) and notifications (events) without any external dependencies. It supports:

- **Request/Response** (`IRequest<TResponse>` / `IRequestHandler<TRequest, TResponse>`)
- **Notifications** (`INotification` / `INotificationHandler<TNotification>`)
- **Pipeline Behaviors** (`IPipelineBehavior<TRequest, TResponse>`)
- **Assembly-scanning registration** through a single `AddEBus(...)` call

EBus resolves handlers and behaviors at runtime using generic interfaces and C# `dynamic` binding—no hard-coded `"Handle"` strings.

---

## Table of Contents

- [Features](#features)  
- [Installation](#installation)  
- [Registering EBus in DI](#registering-ebus-in-di)  
- [Core Concepts](#core-concepts)  
  - [Requests & Handlers](#requests--handlers)  
  - [Notifications & Handlers](#notifications--handlers)  
  - [Pipeline Behaviors](#pipeline-behaviors)  
- [Usage Examples](#usage-examples)  
  - [Sending a Request](#sending-a-request)  
  - [Publishing a Notification](#publishing-a-notification)  
  - [Registering Multiple Assemblies](#registering-multiple-assemblies)  
- [Packaging & Publishing](#packaging--publishing)  
- [Versioning & Maintenance](#versioning--maintenance)  
- [Contributing](#contributing)  
- [License](#license)  

---

## Features

1. **Single-method `Send`**  
   - Call `await mediator.Send(request)`  
   - The compiler infers `TResponse` from `request : IRequest<TResponse>`.  
2. **Publish/Subscribe Notifications**  
   - Call `await mediator.Publish(notification)`  
   - All `INotificationHandler<TNotification>` instances run in parallel.  
3. **Pipeline Behaviors**  
   - Implement `IPipelineBehavior<TRequest, TResponse>`, and EBus wraps behaviors around handler invocation automatically.  
4. **Assembly Scanning**  
   - One-line registration:  
     ```csharp
     services.AddEBus(Assembly.GetExecutingAssembly());
     ```  
   - Scans for all `IRequestHandler<,>`, `INotificationHandler<>`, and `IPipelineBehavior<,>`.  
5. **Lightweight & No External Dependencies**  
   - Built purely on `Microsoft.Extensions.DependencyInjection` and .NET Core 9.  

---

## Installation

Install the latest stable package from NuGet:

```bash
dotnet add package EBus --version 1.0.0
```

Or, if referencing a locally built `.nupkg`:

```xml
<PackageReference Include="EBus" Version="1.0.0" />
```

---

## Registering EBus in DI

In any .NET Core / .NET 6+ application (Console, ASP.NET Core, Worker Service, etc.), register EBus by scanning one or more assemblies:

```csharp
using System.Reflection;
using EBus.Registration;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Scan the current assembly for IRequestHandler<,>, INotificationHandler<>, IPipelineBehavior<,>
services.AddEBus(Assembly.GetExecutingAssembly());

// (Optional) Scan additional assemblies:
// services.AddEBus(
//     Assembly.GetExecutingAssembly(),
//     typeof(SomeExternalType).Assembly
// );

var serviceProvider = services.BuildServiceProvider();
```

This single call does all of the following:

1. Registers `IMediator → Mediator`  
2. Registers every `IRequestHandler<TRequest, TResponse>` found  
3. Registers every `INotificationHandler<TNotification>` found  
4. Registers every `IPipelineBehavior<TRequest, TResponse>` found  

---

## Core Concepts

### Requests & Handlers

1. **Define a Request/Command/Query** by implementing `IRequest<TResponse>`:

   ```csharp
   using EBus.Abstractions;

   public class CreateOrderCommand : IRequest<OrderDto>
   {
       public int CustomerId { get; set; }
       public decimal Total { get; set; }
   }
   ```

2. **Implement the Handler** by implementing `IRequestHandler<TRequest, TResponse>`:

   ```csharp
   using EBus.Abstractions;
   using System.Threading;
   using System.Threading.Tasks;

   public class CreateOrderHandler 
       : IRequestHandler<CreateOrderCommand, OrderDto>
   {
       public Task<OrderDto> Handle(
           CreateOrderCommand request, 
           CancellationToken cancellationToken)
       {
           // ...create order logic...
           var newOrder = new OrderDto
           {
               Id = 1234,
               CustomerId = request.CustomerId,
               Total = request.Total
           };
           return Task.FromResult(newOrder);
       }
   }
   ```

3. **Send the Request** via `IMediator`:

   ```csharp
   var mediator = serviceProvider.GetRequiredService<IMediator>();
   var dto = await mediator.Send(new CreateOrderCommand 
   { 
       CustomerId = 42, 
       Total = 99.50m 
   });
   // dto is OrderDto
   ```

---

### Notifications & Handlers

1. **Define a Notification/Event** by implementing `INotification`:

   ```csharp
   using EBus.Abstractions;

   public class OrderPlacedNotification : INotification
   {
       public int OrderId { get; set; }
   }
   ```

2. **Implement Notification Handlers** by implementing `INotificationHandler<TNotification>`:

   ```csharp
   using EBus.Abstractions;
   using System.Threading;
   using System.Threading.Tasks;

   public class SendOrderConfirmationHandler 
       : INotificationHandler<OrderPlacedNotification>
   {
       public Task Handle(
           OrderPlacedNotification notification, 
           CancellationToken cancellationToken)
       {
           Console.WriteLine($"Sending confirmation for Order {notification.OrderId}");
           return Task.CompletedTask;
       }
   }

   public class UpdateInventoryHandler 
       : INotificationHandler<OrderPlacedNotification>
   {
       public Task Handle(
           OrderPlacedNotification notification, 
           CancellationToken cancellationToken)
       {
           Console.WriteLine($"Updating inventory for Order {notification.OrderId}");
           return Task.CompletedTask;
       }
   }
   ```

3. **Publish the Notification**:

   ```csharp
   await mediator.Publish(new OrderPlacedNotification 
   { 
       OrderId = dto.Id 
   });
   ```

All registered `INotificationHandler<OrderPlacedNotification>` instances run in parallel (`Task.WhenAll`).

---

### Pipeline Behaviors

You can insert cross-cutting logic (e.g., logging, validation) by implementing `IPipelineBehavior<TRequest, TResponse>`:

```csharp
using EBus.Abstractions;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken,
        RequestHandlerDelegate<TResponse> next)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var response = await next().ConfigureAwait(false);
        _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return response;
    }
}
```

- **Registration:**  
  If you place this class in any scanned assembly, `AddEBus(...)` will automatically register it as  
  `IPipelineBehavior<TRequest, TResponse>` for all `TRequest`/`TResponse`.  
- **Execution Order:**  
  Behaviors are invoked in registration order, wrapping around the final handler. Each behavior receives a delegate (`next`) to call the next behavior or handler.

---

## Usage Examples

### Sending a Request

```csharp
using EBus.Abstractions;
using EBus.Registration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Threading.Tasks;

public class ProductDto { /* ... */ }

public class GetProductQuery : IRequest<ProductDto>
{
    public int ProductId { get; set; }
}

public class GetProductHandler : IRequestHandler<GetProductQuery, ProductDto>
{
    public Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        // Load product from database or repository...
        var product = new ProductDto { /* fill properties */ };
        return Task.FromResult(product);
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddEBus(Assembly.GetExecutingAssembly());

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var query = new GetProductQuery { ProductId = 100 };
        ProductDto product = await mediator.Send(query);
        Console.WriteLine($"Retrieved product with ID: {product?.Id}");
    }
}
```

---

### Publishing a Notification

```csharp
using EBus.Abstractions;
using EBus.Registration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Threading.Tasks;

public class OrderPlacedNotification : INotification
{
    public int OrderId { get; set; }
}

public class SendEmailHandler : INotificationHandler<OrderPlacedNotification>
{
    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Sending email for Order {notification.OrderId}");
        return Task.CompletedTask;
    }
}

public class UpdateAnalyticsHandler : INotificationHandler<OrderPlacedNotification>
{
    public Task Handle(OrderPlacedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Updating analytics for Order {notification.OrderId}");
        return Task.CompletedTask;
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddEBus(Assembly.GetExecutingAssembly());

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await mediator.Publish(new OrderPlacedNotification { OrderId = 200 });
        // Both SendEmailHandler and UpdateAnalyticsHandler run in parallel.
    }
}
```

---

### Registering Multiple Assemblies

If your handlers are spread across several class libraries, pass multiple assemblies:

```csharp
using EBus.Registration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

var services = new ServiceCollection();

// Scan current assembly + external assemblies
services.AddEBus(
    Assembly.GetExecutingAssembly(),
    typeof(SomeExternalHandler).Assembly,
    typeof(AnotherModuleMarker).Assembly
);

var provider = services.BuildServiceProvider();
var mediator = provider.GetRequiredService<IMediator>();
```

---

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork the Repository**  
2. **Create a Feature Branch** (`git checkout -b feature/YourFeatureName`)  
3. **Commit Your Changes** (`git commit -m "Add some feature"`)  
4. **Push to Your Fork** (`git push origin feature/YourFeatureName`)  
5. **Open a Pull Request** against the `main` branch

Include:

- A clear description of your change.  
- Any relevant examples or tests.  
- Updates to this README.md if needed.

---

## License

EBus is licensed under the [MIT License](LICENSE). Feel free to use, modify, and distribute under the terms of MIT.
