using System;
using System.Globalization;
using System.Reflection;
using System.Security.Permissions;

namespace OctopusDeployNuGetFeed
{
    public class SizedReference : IDisposable
    {
        private static readonly Type SystemSizedReference = Type.GetType("System.SizedReference", true, false);
        private readonly object _sizedRef;

        public SizedReference(object target)
        {
            _sizedRef = Activator.CreateInstance(SystemSizedReference, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, new object[1]
            {
                target
            }, null);
        }

        public long ApproximateSize
        {
            [PermissionSet(SecurityAction.Assert, Unrestricted = true)] get { return (long) SystemSizedReference.InvokeMember(nameof(ApproximateSize), BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty, null, _sizedRef, null, CultureInfo.InvariantCulture); }
        }

        [PermissionSet(SecurityAction.Assert, Unrestricted = true)]
        public void Dispose()
        {
            SystemSizedReference.InvokeMember(nameof(Dispose), BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null, _sizedRef, null, CultureInfo.InvariantCulture);
        }
    }
}