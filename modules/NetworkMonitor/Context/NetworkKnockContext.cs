using Autofac;
using MadWizard.Desomnia.Network.Configuration;
using MadWizard.Desomnia.Network.Configuration.Knocking;
using MadWizard.Desomnia.Network.Context.Parameters;
using MadWizard.Desomnia.Network.Knocking;
using MadWizard.Desomnia.Network.Knocking.Events;
using MadWizard.Desomnia.Network.Knocking.Filter;
using MadWizard.Desomnia.Network.Knocking.Filter.Rules;
using MadWizard.Desomnia.Network.Knocking.Secrets;
using MadWizard.Desomnia.Network.Neighborhood;
using MadWizard.Desomnia.Network.Services.Knocking;
using Microsoft.Extensions.Logging;
using NetTools;
using System.Net;

namespace MadWizard.Desomnia.Network.Context
{
    internal class NetworkKnockContext : FilterContext
    {
        public required ILogger<NetworkKnockContext> Logger { private get; init; }

        readonly IList<ILifetimeScope> _scopes = [];

        public IEnumerable<KnockStanza> Stanzas => field ??= _scopes.Select(scope => scope.Resolve<KnockStanza>());

        readonly NetworkSegment _targetNetwork;
        readonly NetworkHostRange _targetRange;

        public NetworkKnockContext(ILifetimeScope parent, NetworkMonitorConfig network, DynamicHostRangeInfo config) : base(parent, "knock")
        {
            var port = new IPPort(config.KnockProtocol ?? network.KnockProtocol, config.KnockPort ?? network.KnockPort);

            _targetNetwork = parent.Resolve<NetworkSegment>();
            _targetRange = parent.ResolveNamed<NetworkHostRange>(config.Name!);

            foreach (var secret in config.SharedSecret)
            {
                var scope = parent.BeginLifetimeScope(MatchingScopeLifetimeTags.KnockLifetimeScopeTag, builder =>
                {
                    var label = $"{config.Name}{(secret.Label != null ? $"::{secret.Label}" : "")}"; // maybe mit index?

                    var stanza = builder.RegisterType<KnockStanza>()
                        .WithParameter(TypedParameter.From(label))
                        .WithParameter(TypedParameter.From(port))
                        .WithParameter(TypedParameter.From(BuildSharedSecret(secret)))
                        .WithParameter(TypedNamedResolvedParameter<IKnockDetector>.FindBy(config.KnockMethod ?? network.KnockMethod))
                        .WithParameter(TypedParameter.From(config.KnockTimeout ?? network.KnockTimeout))
                        .SingleInstance()
                        .AsSelf();

                    stanza.OnActivated(args => args.Instance.Knocked += KnockStanza_Knocked);

                    RegisterPacketFilter(builder, config);
                    RegisterKnockFilter(builder, config);
                });

                parent.Disposer.AddInstanceForDisposal(scope);

                _scopes.Add(scope);
            }

            parent.UseTrafficType(port);
        }

        #region Filter Registration
        private void RegisterPacketFilter(ContainerBuilder builder, DynamicHostRangeInfo config)
        {
            RegisterTaggedPacketRuleFilter(builder);

            RegisterHostFilters(builder, config.HostFilterRule);
            RegisterHostRangeFilters(builder, config.HostRangeFilterRule);
        }

        private void RegisterKnockFilter(ContainerBuilder builder, DynamicHostRangeInfo config)
        {
            if (config.ProofIP)
            {
                builder.RegisterType<KnockSourceIPFilter>().As<IKnockFilter>()
                    .SingleInstance();
            }

            if (config.ProofTime is TimeSpan time)
            {
                builder.RegisterType<KnockTimeFilter>().As<IKnockFilter>()
                    .WithParameter(TypedParameter.From(time))
                    .SingleInstance();
            }


            if (config.ServiceFilterRule.Any())
            {
                builder.RegisterType<KnockRuleFilter>().As<IKnockFilter>()
                    .SingleInstance();

                foreach (var filter in config.ServiceFilterRule)
                {
                    builder.RegisterType<KnockPortFilterRule>()
                        .WithParameter(TypedParameter.From(filter.Type))
                        .WithParameter(TypedParameter.From(filter.Protocol))
                        .WithParameter(TypedParameter.From(filter.Port))
                        .As<KnockFilterRule>()
                        .SingleInstance();
                }
            }

            builder.RegisterComposite<CompositeKnockFilter, IKnockFilter>();
        }
        #endregion

        private async void KnockStanza_Knocked(object? sender, KnockEventArgs args)
        {
            var stanza = (KnockStanza)sender!;

            var ip = args.Knock.SourceAddress;

            var range = new IPAddressRange(ip);

            if (_targetRange.AddAddressRange(range))
            {
                _targetNetwork.RememberHostName(ip, stanza.Label, args.Timeout);

                await Task.Delay(args.Timeout); // TODO really naive implementation

                _targetRange.RemoveAddressRange(range);
            }
        }

        private static SharedSecret BuildSharedSecret(SharedSecretData data)
        {
            byte[]? key = null;
            byte[]? authKey = null;

            DigestType authType = default;

            string defaultEncoding = data.Encoding ?? "UTF-8";

            if (data.Key is KeyData keyData)
            {
                key = SharedSecret.TryConvert(keyData.Text, keyData.Encoding ?? defaultEncoding);

                if (data.AuthKey is AuthKeyData authKeyData)
                {
                    authKey = SharedSecret.TryConvert(authKeyData.Text, authKeyData.Encoding ?? defaultEncoding);
                    authType = authKeyData.Type;
                }
            }
            else
            {
                key = SharedSecret.TryConvert(data.Text, defaultEncoding);
            }

            return new SharedSecret(key ?? throw new Exception($"Invalid SecretKey = '{data.Label}'"), authKey, authType);
        }
    }
}
