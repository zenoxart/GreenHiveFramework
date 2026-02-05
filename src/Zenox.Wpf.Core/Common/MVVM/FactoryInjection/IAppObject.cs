using Zenox.NET.Core.Common.Permission;

namespace Zenox.NET.Core.Common.MVVM.FactoryInjection
{
    public interface IAppObject
    {
        public AppLogManager Logger { get; set; }

        AppKontext AppKontext { get; set; }

        void OnFehlerAufgetreten(
                        FehlerAufgetretenEventArgs e);

        void StartMelden([System.Runtime.CompilerServices.CallerMemberName] string aufrufer = null!);

        void EndeMelden([System.Runtime.CompilerServices.CallerMemberName] string aufrufer = null!);

        PermissionType AktuelleBerechtigung { get; set; }
    }
}
