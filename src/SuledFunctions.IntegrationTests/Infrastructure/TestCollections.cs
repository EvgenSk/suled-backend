namespace SuledFunctions.IntegrationTests.Infrastructure;

/// <summary>
/// Collection definition for integration tests that share Azurite fixture
/// </summary>
[CollectionDefinition("Azurite")]
public class AzuriteCollection : ICollectionFixture<AzuriteFixture>
{
    // This class has no code, and is never created. 
    // Its purpose is simply to be the place to apply [CollectionDefinition]
}

/// <summary>
/// Collection definition for integration tests that share Cosmos DB fixture
/// </summary>
[CollectionDefinition("CosmosDb")]
public class CosmosDbCollection : ICollectionFixture<CosmosDbFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition]
}

/// <summary>
/// Collection definition for integration tests that need both Azurite and Cosmos DB
/// </summary>
[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<AzuriteFixture>, ICollectionFixture<CosmosDbFixture>
{
    // This class has no code, and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition]
}
