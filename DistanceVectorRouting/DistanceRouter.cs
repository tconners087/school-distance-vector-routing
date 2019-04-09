using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;

//AUTHOR: Taylor Conners
namespace DistanceVectorRouting
{
    /// <summary>
    /// Abstract class designed to extend to both Distance Routing simulations and actual distance router protocol.
    /// Currently only extends to one class: DistanceRouter_Simulation.cs.
    /// </summary>
    abstract class DistanceRouter
    {
        protected Dictionary<char, Dictionary<char, Node>> distanceVectors;
        protected int numNeighbors;

        protected UdpClient rcv_udpClient;
        protected UdpClient snd_udpClient;
        
        protected Thread server;
        protected IPEndPoint endPoint;
        protected IPEndPoint brdcstEndPoint;

        protected abstract byte[] ConstructPayload(Dictionary<char, Node> values, char destinationMAC, byte[] timeStamp);
        protected abstract void RoutingServer();
        protected abstract void InterpretBytes(byte[] rcvBytes);
        public abstract void StartServer();
        protected abstract void UpdateRoutingTable();

        /// <summary>
        /// Prints routing table.
        /// </summary>
        /// <param name="routingTable"></param>
        /// <param name="address"></param>
        protected void PrintRoutingTable(Dictionary<char, Dictionary<char,Node>> routingTable, char address)
        {
            Console.WriteLine("\n########### - {0} - ###########", address);
            foreach (KeyValuePair<char, Dictionary<char, Node>> row in routingTable)
            {
                Console.WriteLine("FROM {0}: ", row.Key);
                if (row.Value != null && row.Value.Count != 0)
                {
                    foreach (KeyValuePair<char, Node> vector in row.Value)
                    {
                        Console.WriteLine("    <TO: {0}, NEXT HOP NODE: {1}, COST: {2}> ", vector.Value.To, vector.Value.Jump, vector.Value.Cost);
                    }
                }
                else
                {
                    Console.Write("Distance Vectors for Node {0} are unknown.\n", row.Key);
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Returns the local IP address of this device.
        /// </summary>
        /// <returns>IPAddress</returns>
        protected static IPAddress GetLocalIPAddress()
        {
            try
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress address in host.AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return address;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                Console.WriteLine("Network not available, could not retrieve local IP.");
                return null;
            }

            Console.WriteLine("Could not retrieve local IP address despite active network connection");
            return null;
        }
    }
}
