using Map.Models;
using Neo4j.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<MapService>();

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    return GraphDatabase.Driver(config["GraphDatabase:Url"],
        AuthTokens.Basic(config["GraphDatabase:UserName"], config["GraphDatabase:Password"]));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.MapScalarApireference();
}

app.MapPost("/city", async (MapService service, City requset) =>
{
    await service.AddCityASync(requset);
});

app.MapGet("/city", async (MapService service) =>
{
    var result = await service.GetAllCities();

    return Results.Ok(result);
});

app.MapPost("/path", async (MapService service, string source, string destination, int distance) =>
{
    await service.AddPathAsync(source, destination, distance);
});

app.MapGet("/path/distance", async (MapService service, string source, string destination, int distance) =>
{
    var result = await service.GetAllPathsWithDistinationAsync();

    return Results.Ok(result);
});

app.MapGet("/path", async (MapService service, string source, string destination, int distance) =>
{
    var result = await service.GetAllPathsAsync();

    return Results.Ok(result);
});

app.MapGet("/road", async (MapService service, string source, string destination) =>
{
    var result = await service.GetPathsAsync(source, destination);

    return Results.Ok(result);
});


app.MapGet("/path/shortest", async (MapService service, string source, string destination) =>
{
    var result = await service.GetShortestPathAsync(source, destination);

    return Results.Ok(result);
});
app.UseHttpsRedirection();

app.Run();

public class MapService(IDriver driver)
{
    private readonly IDriver _driver = driver;

    public async Task AddCityASync(City request)
    {
        var insertQuery = @"
            CREATE (:City {name
            ";

        using var session = _driver.AsyncSession();

        await session.RunAsync(insertQuery, new
        {
            name = request.Name,
            population = request.Population
        });
    }
    public async Task<List<City>> GetAllCities()
    {
        var getAllQuery = @"
            MATCH (city:City)
            RETURN  city.name as name,city.population as population
            ";

        using var session = _driver.AsyncSession();

        var result = await session.RunAsync(getAllQuery);
        var cities = new List<City>();
        await result.ForEachAsync(record =>
        {
            cities.Add(new City(record["name"].As<string>(),
                record["population"].As<int>()));
        });

        return cities;
    }

    internal async Task AddPathAsync(string source, string destination, int distance)
    {
        var addPathQuery = @"
            MATCH (src:City {name :$source}), (dest:City {name:$desitnation})
            MERGE (src)-[r:ROAD {distance:$distance}]->(dest)
        ";

        using var session = _driver.AsyncSession();

        await session.RunAsync(addPathQuery, new
        {
            source,
            destination,
            distance
        });
    }

    internal async Task<List<int>> GetAllPathsWithDistinationAsync()
    {
        var getAllQuery = @"
            MATCH [road:ROAD]
            RETURN road.distance as roadDistance
            ";

        using var session = _driver.AsyncSession();

        var result = await session.RunAsync(getAllQuery);
        var paths = new List<int>();
        await result.ForEachAsync(record =>
        {
            paths.Add(record["roadDistance"].As<int>());
        });

        return paths;
    }

    internal async Task<List<Map.Models.Path>> GetAllPathsAsync()
    {
        var getAllQuery = @"
            MATCH (src:City)-[road:ROAD]->(dest:City)
            RETURN src.name as srcName,dest.name as destName,road.distance as roadDistance
            ";

        using var session = _driver.AsyncSession();

        var result = await session.RunAsync(getAllQuery);
        var paths = new List<Map.Models.Path>();
        await result.ForEachAsync(record =>
        {
            paths.Add(
                new Map.Models.Path(record["srcName"].As<string>(),
                record["destName"].As<string>(),
                record["roadDistance"].As<int>()));
        });

        return paths;
    }

    internal async Task<object?> GetShortestPathAsync(string source, string destination)
    {

        var getAllQuery = @"
            MATCH path= shortestPath ((src:City {name: $source)-[*]->(dest:City {name: $destination}))
            RETURN  path
            ";

        using var session = _driver.AsyncSession();

        var result = await session.RunAsync(getAllQuery, new { source, destination });

        var paths = new List<string>();
        await result.ForEachAsync(record =>
        {
            var path = record["path"].As<IPath>();
            var route = string.Join("  -> ", path.Nodes.Select(node => node["name"].As<string>()));
            paths.Add(route);
        });

        return paths;
    }

    internal async Task<object?> GetPathsAsync(string source, string destination)
    {

        var getAllQuery = @"
            MATCH path = (src:City {name:$source})-[r:ROAD*]->(dest:City {name: $destination})
            RETURN path 
            ";

        using var session = _driver.AsyncSession();

        var result = await session.RunAsync(getAllQuery, new { source, destination });

        var paths = new List<string>();
        await result.ForEachAsync(record =>
        {
            var path = record["path"].As<IPath>();
            var route = string.Join("  -> ", path.Nodes.Select(node => node["name"].As<string>()));
            paths.Add(route);
        });

        return paths;
    }
}