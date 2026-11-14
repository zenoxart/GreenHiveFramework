using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using GreenHive.NET.Core.Common;
using GreenHive.NET.Core.Common.MVVM.FactoryInjection;
using GreenHive.NET.Core.Common.Permission;
using GreenHive.NET.Core.Common.Services;

namespace GreenHive.NET.Core.Common.Connection
{
    /// <summary>
    /// Stellt Methoden für generische GraphQL-Requests bereit.
    /// </summary>
    public class GraphQLService : AppObject
    {

        /// <summary>
        /// Stellt den HttpClient für GraphQL-Requests bereit.
        /// </summary>
        public HttpClient HttpClient { get; init; } = new();
        public AppKontext Kontext { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void EndeMelden([CallerMemberName] string aufrufer = null)
        {
            throw new NotImplementedException();
        }

        public void OnFehlerAufgetreten(FehlerAufgetretenEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Führt eine GraphQL-Query aus und gibt das Ergebnis als Typ T zurück.
        /// </summary>
        public async Task<T> QueryAsync<T>(string url, string query, object variables = null)
        {
            try
            {
                var request = new
                {
                    query,
                    variables
                };
                var response = await HttpClient.PostAsJsonAsync(url, request);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    return JsonSerializer.Deserialize<T>(data.GetRawText());
                }
                return default;
            }
            catch (Exception ex)
            {
                ExceptionHandler.Logger?.LogException(ex, "GraphQLService.QueryAsync");
                Logger?.LogException(ex, "GraphQLService.QueryAsync");
                return default;
            }
        }

        public void StartMelden([CallerMemberName] string aufrufer = null)
        {
            throw new NotImplementedException();
        }
    }
}
