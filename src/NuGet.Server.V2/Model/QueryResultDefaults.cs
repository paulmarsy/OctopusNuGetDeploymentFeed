using System.Web.Http.OData.Query;

namespace NuGet.Server.V2.Model
{
    public static class QueryResultDefaults
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2211:NonConstantFieldsShouldNotBeVisible")]
        public static ODataQuerySettings DefaultQuerySettings = new ODataQuerySettings()
        {
            HandleNullPropagation = HandleNullPropagationOption.False,
            EnsureStableOrdering = true,
            EnableConstantParameterization = false
        };
    }
}