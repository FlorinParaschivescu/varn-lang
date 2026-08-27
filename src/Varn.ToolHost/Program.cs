using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Varn.Adapter;
using Varn.ToolHost;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton<VarnToolService>();
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "varn",
            Title = "Varn local tool host",
            Version = "0.1.0"
        };
        options.ServerInstructions =
            "Use varn_check before varn_inspect or varn_run. Run only when execution is needed. " +
            "Every run must include explicit allowedCapabilities, maxSteps, and maxOutputCharacters. " +
            "Grant the smallest capability set; this host exposes built-in modules only. " +
            "Varn uses newline-delimited statements and end-delimited blocks, not braces. Return is ret. " +
            "Minimal shape: budget[steps=100] then fn main()->i64, statements, ret 0, end. " +
            "Bindings are named: let total:i64 0 declares an immutable one, var total:i64 0 a mutable one, " +
            "set total add(total,1) assigns, and loop step:i64 from 0 to 4 max 4 ... end is a bounded loop. " +
            "Use i64? for an optional type, some(42) or none[i64] to construct it, and " +
            "if let value:i64 answer ... else ... end to safely bind a present value. " +
            "Use list[i64](1,2,3) for a homogeneous list, list.length(values) for its length, " +
            "list.get(values,1) for an i64?, and each value:i64 in values max 3 ... end for bounded traversal. " +
            "Declare a closed record at program level with rec Order(items:list[i64],tier:str), construct it as " +
            "rec[Order](items=list[i64](1,2),tier=\"gold\") with every declared field set exactly once, and " +
            "read a field with order.items, chaining through nesting as order.home.city. Records are immutable " +
            "and have no dynamic property access. A record field, a list element, and an optional may all be " +
            "another declared record, so list[Line] and rec Cart(lines:list[Line]) are how structured input " +
            "arrives. Build a list with var skus:list[str] list[str]() then set skus list.append(skus,sku); append " +
            "returns a new list. Put a value into a message with str.from_i64. A function needs no unreachable " +
            "trailing ret when every branch already returns. " +
            "To accept host data, give main one record parameter: fn main(order:Order)->Settlement. Never write the " +
            "data into the source and never regenerate the program per input. varn_check reports that contract as " +
            "contract.input; send matching JSON as the varn_run input argument and the same program runs unchanged " +
            "for every input. Input is validated before execution, so a rejected input consumes zero steps. " +
            "Every callable operation: add sub mul div mod min max abs (i64/f64), and or not (bool), " +
            "eq ne (i64/f64/bool/str), lt gt lte gte (i64/f64/str), str.length str.concat str.contains " +
            "str.to_lower str.to_upper str.from_i64 str.from_f64 str.from_bool " +
            "str.starts_with str.ends_with, list.length list.get list.append list.contains, io.print. There are no " +
            "operators, so write and(gte(total,10),eq(tier,\"gold\")) rather than total>=10 && tier==\"gold\". " +
            "Both operands of and/or are always evaluated. Nothing else exists: do not invent a function. " +
            "For a failure a caller must handle, use result[T]: build it with ok(value) or " +
            "err[T](\"message\"), and read it with if ok value:i64 <expr> ... else err reason:str ... end. " +
            "num.div num.mod num.to_i64 str.to_i64 str.to_f64 return result[T]; plain div and mod trap on a " +
            "zero divisor, so use num.div when the divisor is data. main may return result[T]: a failed " +
            "result still reports success with no diagnostics, because the run completed and the rule did not hold.";
    })
    .WithStdioServerTransport()
    .WithTools<VarnMcpTools>();

await builder.Build().RunAsync().ConfigureAwait(false);
