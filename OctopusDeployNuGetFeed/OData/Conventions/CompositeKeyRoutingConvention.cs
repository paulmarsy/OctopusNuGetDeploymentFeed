using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Controllers;
using System.Web.Http.OData.Routing;
using System.Web.Http.OData.Routing.Conventions;

namespace OctopusDeployNuGetFeed.OData.Conventions
{
    /// <summary>
    ///     Adds support for composite keys in OData requests (e.g. (Id='',Version=''))
    /// </summary>
    public class CompositeKeyRoutingConvention
        : EntityRoutingConvention
    {
        private static readonly char[] KeyTrimChars = {' ', '\''};

        public override string SelectAction(ODataPath odataPath, HttpControllerContext controllerContext, ILookup<string, HttpActionDescriptor> actionMap)
        {
            var routeValues = controllerContext.RouteData.Values;

            var action = base.SelectAction(odataPath, controllerContext, actionMap);
            if (action != null)
            {
                if (routeValues.ContainsKey(ODataRouteConstants.Key))
                {
                    var keyRaw = routeValues[ODataRouteConstants.Key] as string;
                    if (keyRaw != null)
                        TryEnrichRouteValues(keyRaw, routeValues);
                }
            }
            //Allows actions for an entity with composite key
            else if (odataPath.PathTemplate == "~/entityset/key/action" ||
                     odataPath.PathTemplate == "~/entityset/key/cast/action")
            {
                var keyValueSegment = odataPath.Segments[1] as KeyValuePathSegment;
                var actionSegment = odataPath.Segments.Last() as ActionPathSegment;
                var actionFunctionImport = actionSegment.Action;

                controllerContext.RouteData.Values[ODataRouteConstants.Key] = keyValueSegment.Value;
                TryEnrichRouteValues(keyValueSegment.Value, routeValues);
                return actionFunctionImport.Name;
            }

            return action;
        }

        public static bool TryEnrichRouteValues(string keyRaw, IDictionary<string, object> routeValues)
        {
            IEnumerable<string> compoundKeyPairs = keyRaw.Split(',');
            if (!compoundKeyPairs.Any())
                return false;

            foreach (var compoundKeyPair in compoundKeyPairs)
            {
                var pair = compoundKeyPair.Split('=');
                if (pair.Length != 2)
                    continue;
                var keyName = pair[0].Trim(KeyTrimChars);
                var keyValue = pair[1].Trim(KeyTrimChars);

                routeValues.Add(keyName, keyValue);
            }
            return true;
        }
    }
}