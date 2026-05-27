``MagicPacket`` = ``g`` ⭐️
  Wakes the system when a Magic Packet addressed to the NIC's MAC is received. The standard Wake-on-LAN method — this is what Desomnia sends to sleeping hosts.

``PHY`` = ``p``
  Wakes on any physical layer event, such as a cable being connected or a link speed change. Very broad; use with caution on always-connected interfaces.

``Unicast`` = ``u``
  Wakes on any unicast packet addressed to the NIC's MAC address. Will trigger on ordinary traffic from any sender.

``Multicast`` = ``m``
  Wakes on multicast packets.

``Broadcast`` = ``b``
  Wakes on broadcast packets, including ARP requests and DHCP traffic.

``ARP`` = ``a``
  Wakes specifically on ARP requests.

``SecureOn`` = ``s``
  Adds password protection to the Magic Packet mechanism. The sender must include the correct six-byte password in the Magic Packet. The password itself is configured in the NIC driver, not in Desomnia.

``Filter`` = ``s``
  Wakes based on custom hardware-level packet filters defined in the NIC driver. The filter rules are driver-specific.