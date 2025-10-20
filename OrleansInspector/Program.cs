using System.Linq;
using System.Reflection;

var clusteringAssembly = Assembly.Load("Orleans.Clustering.Redis");
Console.WriteLine("Types in Orleans.Clustering.Redis:");
foreach (var type in clusteringAssembly.GetExportedTypes().OrderBy(t => t.FullName))
{
	Console.WriteLine($" - {type.FullName}");
}

Console.WriteLine();

var clusteringExtensionsType = clusteringAssembly.GetType("Microsoft.Extensions.Hosting.RedisClusteringISiloBuilderExtensions");
if (clusteringExtensionsType is not null)
{
	Console.WriteLine("RedisClusteringISiloBuilderExtensions methods:");
	foreach (var method in clusteringExtensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
	{
		var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"));
		Console.WriteLine($" - {method.Name}({parameters})");
	}
}

Console.WriteLine();

var clientExtensionsType = clusteringAssembly.GetType("Microsoft.Extensions.Hosting.RedisClusteringIClientBuilderExtensions");
if (clientExtensionsType is not null)
{
	Console.WriteLine("RedisClusteringIClientBuilderExtensions methods:");
	foreach (var method in clientExtensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
	{
		var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"));
		Console.WriteLine($" - {method.Name}({parameters})");
	}
}

Console.WriteLine();

var persistenceAssembly = Assembly.Load("Orleans.Persistence.Redis");
Console.WriteLine("Types in Orleans.Persistence.Redis:");
foreach (var type in persistenceAssembly.GetExportedTypes().OrderBy(t => t.FullName))
{
	Console.WriteLine($" - {type.FullName}");
}

Console.WriteLine();

var clusteringOptionsType = clusteringAssembly.GetType("Orleans.Clustering.Redis.RedisClusteringOptions");
if (clusteringOptionsType is not null)
{
	Console.WriteLine("RedisClusteringOptions properties:");
	foreach (var property in clusteringOptionsType.GetProperties().OrderBy(p => p.Name))
	{
		Console.WriteLine($" - {property.Name} ({property.PropertyType.Name})");
	}
}

Console.WriteLine();

var storageOptionsType = persistenceAssembly.GetType("Orleans.Persistence.RedisStorageOptions");
Console.WriteLine(storageOptionsType is null ? "RedisStorageOptions type not found" : "RedisStorageOptions found");
if (storageOptionsType is not null)
{
	Console.WriteLine("RedisStorageOptions properties:");
	foreach (var property in storageOptionsType.GetProperties().OrderBy(p => p.Name))
	{
		Console.WriteLine($" - {property.Name} ({property.PropertyType.Name})");
	}
}

Console.WriteLine();

var storageExtensionsType = persistenceAssembly.GetType("Orleans.Hosting.RedisSiloBuilderExtensions");
if (storageExtensionsType is not null)
{
	Console.WriteLine("RedisSiloBuilderExtensions methods:");
	foreach (var method in storageExtensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static).OrderBy(m => m.Name))
	{
		var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.FullName} {p.Name}"));
		Console.WriteLine($" - {method.Name}({parameters})");
	}
}
