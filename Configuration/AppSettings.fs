namespace BookManagement.Configuration

/// Configuration records mapping to appsettings.json sections
module AppSettings =

    type CosmosDbSettings =
        {
            ConnectionString : string
            DatabaseName     : string
            ContainerName    : string
            PartitionKey     : string
        }

    type AzureSearchSettings =
        {
            Endpoint  : string
            ApiKey    : string
            IndexName : string
        }

    type JwtSettings =
        {
            Issuer    : string
            Audience  : string
            SecretKey : string
        }
