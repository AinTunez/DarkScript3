using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SoapstoneLib;
using SoapstoneLib.Proto;

namespace TestConsoleApp
{
    internal class MapTest
    {
        public static async Task Run(string[] args)
        {
            // TODO: Add more thorough unit tests here.
            // For now, this is just a Hello World type interaction.
            SoapstoneClient.Provider provider = SoapstoneClient.GetProvider(KnownServer.DSMapStudio);
            if (!provider.TryGetClient(out SoapstoneClient client))
            {
                Console.WriteLine($"Server {provider.Server} not found");
                return;
            }
            ServerInfoResponse info = await client.GetServerInfo();
            Console.WriteLine($"Info response: {info}");
            EditorResource mapResource = new EditorResource { Type = EditorResourceType.Map, Game = FromSoftGame.EldenRing };
            PropertySearch search = PropertySearch.AllOf(
                PropertySearch.AnyOf(
                    new PropertySearch.Condition(PropertyComparisonType.Greater, "EntityID", 0),
                    new PropertySearch.Condition(PropertyComparisonType.Greater, "EntityGroupIDs", 0)),
                new PropertySearch.Condition(PropertyComparisonType.Equal, "Type", "Enemy"));
            RequestedProperties props = new RequestedProperties().Add("EntityID").AddNonTrivial("EntityGroupIDs");
            List<SoulsObject> results = await client.SearchObjects(mapResource, SoulsKey.MsbEntryKey.KeyType, search, props);
            foreach (SoulsObject obj in results)
            {
                Console.WriteLine(obj);
            }
            // Param field access
            EditorResource paramResource = new EditorResource { Type = EditorResourceType.Param, Game = FromSoftGame.EldenRing };
            search = PropertySearch.Of(new PropertySearch.Condition(PropertyComparisonType.Equal, "textId", 7000));
            props = new RequestedProperties().Add("ID", "Name", "textId").AddNonTrivial("isGrayoutForRide", "_invalidProp");
            results = await client.SearchObjects(paramResource, SoulsKey.GameParamRowKey.KeyType, search, props);
            foreach (SoulsObject obj in results)
            {
                Console.WriteLine(obj);
            }
        }
    }
}
