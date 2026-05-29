# API Reference: IO & Register / API 参考：IO 与寄存器

## IO Operations / IO 操作

All IO methods are on `CodroidClient`.

所有 IO 方法均位于 `CodroidClient` 上。

---

### GetDi / 读取数字输入

Read a digital input port. Returns `0` or `1`.

读取数字输入端口。返回 `0` 或 `1`。

```csharp
// Read DI port 0 / 读取 DI 端口 0
int di0 = await robot.GetDi(0);
Console.WriteLine($"DI 0 = {di0}"); // 0 or 1
```

**Signature / 签名：**

```csharp
Task<int> GetDi(int port)
```

---

### GetDo / 读取数字输出

Read the current state of a digital output port. Returns `0` or `1`.

读取数字输出端口的当前状态。返回 `0` 或 `1`。

```csharp
// Read DO port 10 / 读取 DO 端口 10
int do10 = await robot.GetDo(10);
Console.WriteLine($"DO 10 = {do10}");
```

**Signature / 签名：**

```csharp
Task<int> GetDo(int port)
```

---

### GetAi / 读取模拟输入

Read an analog input port. Returns a floating-point value.

读取模拟输入端口。返回浮点值。

```csharp
// Read AI port 1 / 读取 AI 端口 1
double ai1 = await robot.GetAi(1);
Console.WriteLine($"AI 1 = {ai1:F3}");
```

**Signature / 签名：**

```csharp
Task<double> GetAi(int port)
```

---

### GetAo / 读取模拟输出

Read the current value of an analog output port.

读取模拟输出端口的当前值。

```csharp
// Read AO port 2 / 读取 AO 端口 2
double ao2 = await robot.GetAo(2);
Console.WriteLine($"AO 2 = {ao2:F3}");
```

**Signature / 签名：**

```csharp
Task<double> GetAo(int port)
```

---

### SetDo / 写入数字输出

Write a digital output. `value` must be `0` or `1`.

写入数字输出。`value` 必须为 `0` 或 `1`。

```csharp
// Set DO port 10 to ON / 将 DO 端口 10 设为 ON
await robot.SetDo(10, 1);

// Set DO port 10 to OFF / 将 DO 端口 10 设为 OFF
await robot.SetDo(10, 0);
```

**Signature / 签名：**

```csharp
Task<CommonResponse> SetDo(int port, int value)
```

Throws `ArgumentOutOfRangeException` if `value` is not `0` or `1`.
如果 `value` 不是 `0` 或 `1`，抛出 `ArgumentOutOfRangeException`。

---

### SetAo / 写入模拟输出

Write an analog output value.

写入模拟输出值。

```csharp
// Set AO port 2 to 3.14 / 将 AO 端口 2 设为 3.14
await robot.SetAo(2, 3.14);
```

**Signature / 签名：**

```csharp
Task<CommonResponse> SetAo(int port, double value)
```

---

### GetIoValues / 批量读取 IO

Batch-read multiple IO pins in a single request. Returns the raw `CommonResponse` whose `db` is a JSON array.

在一次请求中批量读取多个 IO 点。返回原始 `CommonResponse`，其 `db` 为 JSON 数组。

```csharp
// Batch read DI0, DO10, AI1, AO2 / 批量读取 DI0、DO10、AI1、AO2
var resp = await robot.GetIoValues(new List<(string Type, int Port)>
{
    (IoPortKind.Di, 0),
    (IoPortKind.Do, 10),
    (IoPortKind.Ai, 1),
    (IoPortKind.Ao, 2),
});
Console.WriteLine(resp.db.GetRawText());

// Parse individual values / 解析单个值
int di0 = IoGetResponseParser.ParseDigital(resp, IoPortKind.Di, 0);
double ao2 = IoGetResponseParser.ParseAnalog(resp, IoPortKind.Ao, 2);
```

**Signature / 签名：**

```csharp
Task<CommonResponse> GetIoValues(IReadOnlyList<(string Type, int Port)> pins)
```

---

## IO Helper Types / IO 辅助类型

### IoPortKind

Constants for the `type` field in IOManager protocol.

IOManager 协议中 `type` 字段的常量。

| Constant / 常量 | Value / 值 | Description / 说明 |
|---|---|---|
| `IoPortKind.Di` | `"DI"` | Digital input / 数字输入 |
| `IoPortKind.Do` | `"DO"` | Digital output / 数字输出 |
| `IoPortKind.Ai` | `"AI"` | Analog input / 模拟输入 |
| `IoPortKind.Ao` | `"AO"` | Analog output / 模拟输出 |

### IoGetResponseParser

Parses the `db` field from `GetIoValues` responses.

解析 `GetIoValues` 响应中的 `db` 字段。

```csharp
// Parse a digital value / 解析数字量值
int di = IoGetResponseParser.ParseDigital(response, IoPortKind.Di, 0);

// Parse an analog value / 解析模拟量值
double ai = IoGetResponseParser.ParseAnalog(response, IoPortKind.Ai, 1);

// Build a query payload / 构建查询载荷
var pins = new List<(string, int)> { (IoPortKind.Di, 0), (IoPortKind.Do, 10) };
JsonElement query = IoGetResponseParser.BuildGetQuery(pins);
```

| Method / 方法 | Returns / 返回 | Description / 说明 |
|---|---|---|
| `ParseDigital(response, ioType, port)` | `int` | Extract `0` or `1` for DI/DO / 提取 DI/DO 的 `0` 或 `1` |
| `ParseAnalog(response, ioType, port)` | `double` | Extract floating-point value for AI/AO / 提取 AI/AO 的浮点值 |
| `BuildGetQuery(pins)` | `JsonElement` | Build JSON array for batch IO query / 构建批量 IO 查询的 JSON 数组 |

---

## Register Operations / 寄存器操作

All register methods are on `CodroidClient`.

所有寄存器方法均位于 `CodroidClient` 上。

---

### GetRegisterValue / 读取单个寄存器

Read a single register. The returned `RegisterReadValue` contains the address and a raw JSON value. Use `GetInt32()` or `GetDouble()` to convert.

读取单个寄存器。返回的 `RegisterReadValue` 包含地址和原始 JSON 值。使用 `GetInt32()` 或 `GetDouble()` 进行转换。

```csharp
// Read register at address 49100 / 读取地址 49100 的寄存器
RegisterReadValue reg = await robot.GetRegisterValue(49100);

// Try as integer / 尝试读为整数
if (reg.TryGetInt32(out int intVal))
    Console.WriteLine($"Address {reg.Address} = {intVal} (int)");
else
    Console.WriteLine($"Address {reg.Address} = {reg.GetDouble()} (float)");
```

**Signature / 签名：**

```csharp
Task<RegisterReadValue> GetRegisterValue(int address)
```

---

### GetRegisterValues / 批量读取寄存器

Read multiple registers in a single request. The returned list order matches the input addresses.

在一次请求中读取多个寄存器。返回列表的顺序与输入地址一致。

```csharp
// Batch read registers 49100, 49102, 49104 / 批量读取寄存器 49100、49102、49104
var regs = await robot.GetRegisterValues(new[] { 49100, 49102, 49104 });

foreach (var r in regs)
{
    if (r.TryGetInt32(out int v))
        Console.WriteLine($"  Address {r.Address}: {v}");
    else
        Console.WriteLine($"  Address {r.Address}: {r.GetDouble():G}");
}
```

**Signature / 签名：**

```csharp
Task<IReadOnlyList<RegisterReadValue>> GetRegisterValues(IReadOnlyList<int> addresses)
```

---

### SetRegisterValue (int) / 写入寄存器整型值

Write an integer value to a register.

向寄存器写入整型值。

```csharp
// Write 520 to register 49100 / 向寄存器 49100 写入 520
await robot.SetRegisterValue(49100, 520);

// Write 0 to clear / 写入 0 以清零
await robot.SetRegisterValue(49100, 0);
```

**Signature / 签名：**

```csharp
Task<CommonResponse> SetRegisterValue(int address, int value)
```

---

### SetRegisterValue (double) / 写入寄存器浮点值

Write a floating-point value to a register.

向寄存器写入浮点值。

```csharp
// Write 520.52 to register 49300 / 向寄存器 49300 写入 520.52
await robot.SetRegisterValue(49300, 520.52);

// Write 0.0 to clear / 写入 0.0 以清零
await robot.SetRegisterValue(49300, 0.0);
```

**Signature / 签名：**

```csharp
Task<CommonResponse> SetRegisterValue(int address, double value)
```

---

### SetExtendArrayType / 设置扩展数组元素类型

Set the data type of an extend-array element. Index range: 0~999.

设置扩展数组元素的数据类型。索引范围：0~999。

```csharp
// Set element 0 to Int32 / 将元素 0 设为 Int32 类型
await robot.SetExtendArrayType(0, RegisterExtendArrayValueType.Int32);

// Set element 5 to Float32 / 将元素 5 设为 Float32 类型
await robot.SetExtendArrayType(5, RegisterExtendArrayValueType.Float32);
```

**Signature / 签名：**

```csharp
Task<CommonResponse> SetExtendArrayType(int index, string type)
```

Throws `ArgumentOutOfRangeException` if `index` is not in 0~999.
如果 `index` 不在 0~999 范围内，抛出 `ArgumentOutOfRangeException`。

Throws `ArgumentException` if `type` is not a recognized value.
如果 `type` 不是受支持的类型值，抛出 `ArgumentException`。

---

### RemoveExtendArray / 删除扩展数组元素

Remove an extend-array element and reset its data. Index range: 0~999.

删除扩展数组元素并重置其数据。索引范围：0~999。

```csharp
// Remove element 0 / 删除元素 0
await robot.RemoveExtendArray(0);
```

**Signature / 签名：**

```csharp
Task<CommonResponse> RemoveExtendArray(int index)
```

---

## RegisterReadValue Struct / RegisterReadValue 结构体

Holds the result of a register read: address and raw JSON value.

保存寄存器读取结果：地址和原始 JSON 值。

| Property / 属性 | Type / 类型 | Description / 说明 |
|---|---|---|
| `Address` | `int` | Register address / 寄存器地址 |
| `Value` | `JsonElement` | Raw JSON value from the controller / 控制器返回的原始 JSON 值 |

| Method / 方法 | Returns / 返回 | Description / 说明 |
|---|---|---|
| `GetInt32()` | `int` | Read as integer; throws if not convertible / 读为整数；不可转换时抛出异常 |
| `GetDouble()` | `double` | Read as floating-point / 读为浮点数 |
| `TryGetInt32(out int value)` | `bool` | Try read as integer without throwing / 尝试读为整数，不抛异常 |

```csharp
RegisterReadValue reg = await robot.GetRegisterValue(49100);

// Safe integer read / 安全整数读取
if (reg.TryGetInt32(out int v))
    Console.WriteLine(v);

// Read as double / 读为浮点
double d = reg.GetDouble();
```

---

## RegisterExtendArrayValueType Constants / RegisterExtendArrayValueType 常量

Supported data types for extend-array elements.

扩展数组元素支持的数据类型。

| Constant / 常量 | Wire Value / 协议值 |
|---|---|
| `RegisterExtendArrayValueType.Bool` | `"Bool"` |
| `RegisterExtendArrayValueType.UInt8` | `"UInt8"` |
| `RegisterExtendArrayValueType.Int8` | `"Int8"` |
| `RegisterExtendArrayValueType.UInt16` | `"UInt16"` |
| `RegisterExtendArrayValueType.Int16` | `"Int16"` |
| `RegisterExtendArrayValueType.UInt32` | `"UInt32"` |
| `RegisterExtendArrayValueType.Int32` | `"Int32"` |
| `RegisterExtendArrayValueType.Float32` | `"Float32"` |

---

## Full Example: IO & Register Read-Write / 完整示例：IO 与寄存器读写

```csharp
using Codroid;

ConsoleUtf8.InitConsoleUtf8();

var robot = new CodroidClient("192.168.8.136");

try
{
    await robot.ConnectRemoteAndSwitchOn();

    // --- IO ---
    // Read DI and mirror to DO / 读取 DI 并镜像到 DO
    int di0 = await robot.GetDi(0);
    Console.WriteLine($"DI 0 = {di0}");
    await robot.SetDo(10, di0);
    Console.WriteLine($"DO 10 set to {di0}");

    // Read analog values / 读取模拟量
    double ai1 = await robot.GetAi(1);
    double ao2 = await robot.GetAo(2);
    Console.WriteLine($"AI 1 = {ai1:F3}, AO 2 = {ao2:F3}");

    // Batch IO / 批量 IO
    var batch = await robot.GetIoValues(new List<(string, int)>
    {
        (IoPortKind.Di, 0),
        (IoPortKind.Do, 10),
    });
    Console.WriteLine("Batch result: " + batch.db.GetRawText());

    // --- Register ---
    // Single read / 单个读取
    RegisterReadValue r = await robot.GetRegisterValue(49100);
    Console.WriteLine($"Register 49100 = {r.GetDouble():G}");

    // Batch read / 批量读取
    var regs = await robot.GetRegisterValues(new[] { 49100, 49102, 49104 });
    foreach (var rv in regs)
        Console.WriteLine($"  {rv.Address}: {rv.GetDouble():G}");

    // Write integer / 写入整型
    await robot.SetRegisterValue(49100, 520);

    // Write float / 写入浮点
    await robot.SetRegisterValue(49300, 520.52);

    // Extend array / 扩展数组
    await robot.SetExtendArrayType(0, RegisterExtendArrayValueType.Int32);
    await robot.RemoveExtendArray(0);
}
finally
{
    robot.Disconnect();
}
```
