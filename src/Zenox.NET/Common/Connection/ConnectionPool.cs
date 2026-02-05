using Zenox.NET.Core.Common.MVVM.FactoryInjection;
using Zenox.NET.Core.Common.MVVM.ViewModelSupport;

namespace Zenox.NET.Core.Common.Connection
{
    /// <summary>
    /// Stellt eine Sammlung von Verbindungsdiensten dar, die für die Kommunikation mit verschiedenen APIs und Diensten verwendet werden.
    /// </summary>
    public class ConnectionPool : NotificationObject
    {
        /// <summary>
        /// Definiert den Anwendungskontext
        /// </summary>
        public new virtual AppKontext AppKontext
        {
            get => base.AppKontext;
            set => base.AppKontext = value;
        }

        public ConnectionPool() : base()
        {
        }

        /// <summary>
        /// Stellt einen Zugriff auf die Rest-API bereit, die für die Kommunikation mit REST Diensten verwendet wird.
        /// </summary>
        public APIService RestApi
        {
            get
            {
                field ??= AppKontext.Produziere<APIService>();
                return field;
            }
            set
            {
                if (value != field)
                {
                    field = value;
                }
            }
        }

        /// <summary>
        /// Stellt einen Zugriff auf die GraphQL-API bereit, die für die Kommunikation mit GraphQL-Diensten verwendet wird.
        /// </summary>
        public GraphQLService GraphQL
        {
            get
            {
                field ??= AppKontext.Produziere<GraphQLService>();
                return field;
            }
            set
            {
                if (value != field)
                {
                    field = value;
                }
            }
        }


        /// <summary>
        /// Stellt einen Zugriff auf den Redis-Dienst bereit, der für die Kommunikation mit Redis-Datenbanken verwendet wird.
        /// </summary>
        public RedisService Redis
        {
            get
            {
                field ??= AppKontext.Produziere<RedisService>();
                return field;
            }
            set
            {
                if (value != field)
                {
                    field = value;
                }
            }
        }

        /// <summary>
        /// Stellt einen Zugriff auf den WebSocket-Dienst bereit, der für die Kommunikation über WebSockets verwendet wird.
        /// </summary>
        public UDPService UDP
        {
            get
            {
                field ??= AppKontext.Produziere<UDPService>();
                return field;
            }
            set
            {
                if (value != field)
                {
                    field = value;
                }
            }
        }
    }
}
