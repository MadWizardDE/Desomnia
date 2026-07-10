using Autofac;
using Autofac.Core.Resolving.Pipeline;
using MadWizard.Desomnia.Network.Naming.Options;
using MadWizard.Desomnia.Network.SleepProxy.Registration;
using Makaretu.Dns;

namespace MadWizard.Desomnia.Network.Middleware
{
    /// <summary>
    /// Builds the wire form of a <see cref="SleepProxyRegistrationMessageBurst"/> from the
    /// <see cref="SleepProxyRegistration"/> and the MTU passed as resolve parameters: the
    /// registration is serialized into one DNS UPDATE and -- when it exceeds the MTU -- split
    /// Apple-style into a burst of smaller updates sharing the original message id. Records that
    /// belong together (a service's PTR/SRV/TXT, an address and its reverse mapping) always stay in
    /// the same message, along with their service-scoped EDNS options; every message repeats the
    /// remaining OPT options (Owner, Lease) and gets its <see cref="EdnsPagingOption"/> re-stamped
    /// (page index / page count) so the receiver knows when the burst is complete.
    /// An MTU of 0 disables splitting: the update is then sent as-is and an oversized one left to
    /// IP fragmentation -- viable on a switched LAN, and received whole through the sleep proxy's
    /// OS socket, which gets fragmented datagrams kernel-reassembled.
    /// </summary>
    public sealed class MTUSleepProxyMessageSplitter : IResolveMiddleware
    {
        public PipelinePhase Phase => PipelinePhase.ParameterSelection;

        public void Execute(ResolveRequestContext context, Action<ResolveRequestContext> next)
        {
            if (context.FirstParameterOfType<SleepProxyRegistration>() is SleepProxyRegistration reg)
            {
                ushort mtu = context.FirstParameterOfType<ushort>();

                context.ChangeParameters([TypedParameter.From(Split((Message)reg, mtu > 0 ? mtu : null))]);
            }

            next(context);
        }

        /// <summary>An indivisible batch of records, plus the EDNS options that belong to them.</summary>
        private record RecordGroup(List<ResourceRecord> Records, List<EdnsServiceOption> Options);

        private static Message[] Split(Message update, ushort? mtu)
        {
            if (mtu == null || update.ToByteArray().Length <= mtu)
                return [update]; // fits (or splitting is disabled): send as-is

            // options every part must repeat; the service-scoped ones travel with their group
            // instead, and the paging option is re-stamped per part
            List<EdnsOption> common = [.. update.Options.Where(option => option is not EdnsServiceOption and not EdnsPagingOption)];

            List<List<RecordGroup>> parts = [];
            List<RecordGroup> current = [];

            foreach (var group in GroupRecords(update))
            {
                current.Add(group);

                // A group that no longer fits moves to the next message; a group alone in its message
                // stays, however large (better one oversized message than an unsendable registration).
                if (current.Count > 1 && Assemble(update, current, common, first: parts.Count == 0, page: 1, count: 1).ToByteArray().Length > mtu)
                {
                    current.RemoveAt(current.Count - 1);

                    parts.Add(current);

                    current = [group];
                }
            }

            parts.Add(current);

            return [.. parts.Select((groups, index) => Assemble(update, groups, common, first: index == 0, page: (byte)(index + 1), count: (byte)parts.Count))];
        }

        /// <summary>
        /// Partitions the update's records into indivisible batches: a service PTR with its instance's
        /// records (SRV/TXT) and its service-scoped options, an address record with its reverse
        /// mapping, anything else on its own.
        /// </summary>
        private static IEnumerable<RecordGroup> GroupRecords(Message update)
        {
            List<ResourceRecord> records = [.. update.AuthorityRecords];
            List<EdnsServiceOption> options = [.. update.Options.OfType<EdnsServiceOption>()];

            while (records.Count > 0)
            {
                var head = records[0];

                RecordGroup group = new([head], []);

                switch (head)
                {
                    case PTRRecord ptr when ptr.IsServicePointer:
                        group.Records.AddRange(records.Skip(1).Where(record => record.Name == ptr.DomainName));
                        group.Options.AddRange(options.Where(option => option.ServiceDomainName == ptr.Name));
                        break;

                    case AddressRecord adr:
                        group.Records.AddRange(records.Skip(1).Where(record => record is PTRRecord reverse
                            && reverse.IsReverseMapping && reverse.Name == adr.Address.ArpaDomainName));
                        break;
                }

                records.RemoveAll(record => group.Records.Any(claimed => ReferenceEquals(claimed, record)));
                options.RemoveAll(option => group.Options.Any(claimed => ReferenceEquals(claimed, option)));

                yield return group;
            }

            // service options without a matching record still travel, in a batch of their own
            if (options.Count > 0)
                yield return new RecordGroup([], options);
        }

        private static Message Assemble(Message update, List<RecordGroup> groups, List<EdnsOption> common, bool first, byte page, byte count)
        {
            var message = new Message
            {
                Id = update.Id,
                Opcode = update.Opcode,
            };

            // the question (zone) section travels only in the head of the burst
            if (first)
                message.Questions.AddRange(update.Questions);

            foreach (var group in groups)
                message.AuthorityRecords.AddRange(group.Records);

            message.AdditionalRecords.Add(new OPTRecord
            {
                Options = [.. common, .. groups.SelectMany(group => group.Options), new EdnsPagingOption { Index = page, Count = count }]
            });

            return message;
        }
    }
}
