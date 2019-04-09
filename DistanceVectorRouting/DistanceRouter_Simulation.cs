using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using System.Collections;
using System.Timers;

//AUTHOR: Taylor Conners
namespace DistanceVectorRouting
{
    class DistanceRouter_Simulation : DistanceRouter
    {
        private int port = 52;
        private Int32 infinity = 0b0111_1111_1000_0000_0000_0000_0000_0000;
        private byte[] costFlag = System.Text.Encoding.ASCII.GetBytes("f");
        private Byte[] receivedBytes;
        private byte[] timeStamp;
        private System.Timers.Timer timer;
        private char ADDRESS_thisNode;
        private Random rnd = new Random();
        private float timeFloat;
        private char receivedNodeAddress;
        private int outputCounter;
        private string filename;
        private Dictionary<char, float> directCosts = new Dictionary<char, float>();

        private bool receivedPkt;
        private bool timeCost;

        /// <summary>
        /// Basic constructor of a DistanceRouter_Simulation. Extends the base class DistanceRouter.
        /// </summary>
        /// <param name="d"></param>
        /// <param name="numNeighbors"></param>
        /// <param name="ADDRESS_thisNode"></param>
        public DistanceRouter_Simulation(Dictionary<char, Dictionary<char, Node>> d, int numNeighbors, char ADDRESS_thisNode, string filename, int port): base()
        {
            if (port != 0)
            {
                this.port = port;
            }

            this.filename = filename;
            Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelKeyPress);
            distanceVectors = d;
            this.numNeighbors = numNeighbors;
            this.ADDRESS_thisNode = ADDRESS_thisNode;

            timer = new System.Timers.Timer();
            timer.Elapsed += new ElapsedEventHandler(TimedBroadcast);
            timer.Interval = 15000;

            brdcstEndPoint = new IPEndPoint(IPAddress.Broadcast, this.port);
            snd_udpClient = new UdpClient();
            snd_udpClient.ExclusiveAddressUse = false;
            snd_udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            
            endPoint = new IPEndPoint(IPAddress.Any, this.port); 
            rcv_udpClient = new UdpClient();
            rcv_udpClient.ExclusiveAddressUse = false;
            rcv_udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            rcv_udpClient.Client.Bind(endPoint);
            receivedBytes = new Byte[5000];
            outputCounter = 0;

            timeCost = false;
            receivedPkt = false;
        }

        /// <summary>
        /// Prints all bytes within a byte array. Used for debugging and not currently called within this program.
        /// </summary>
        /// <param name="p"></param>
        void PrintPayload(byte[] p)
        {
            float testFloat = 0;
            for (int i = 0; i < p.Length; )
            {
                if (p[i] == costFlag[0])
                {
                    if (i + 4 < p.Length /*&& Char.IsLetterOrDigit(Convert.ToChar(p[i+5]))*/)
                    {
                        testFloat = (BitConverter.ToSingle(p, i+1));
                        
                        Console.WriteLine(testFloat);
                        i = i + 5;
                        continue;
                    }
                }
                
                Console.WriteLine(Convert.ToChar(p[i]));
                if (i == 0)
                {
                    i = i + 2;
                    continue;
                }
                Console.WriteLine(Convert.ToChar(p[i + 2]));
                i = i + 4;
            }
        }

        /// <summary>
        /// Constructs a payload containing this node's routing table to be sent to a specific destination node.
        /// </summary>
        /// <param name="values"></param>
        /// <param name="ADDRESS_destinationNode"></param>
        /// <param name="timeStamp"></param>
        /// <returns></returns>
        protected override byte[] ConstructPayload(Dictionary<char, Node> values, char ADDRESS_destinationNode, byte[] timeStamp)
        {
            List<byte> payload_list = new List<byte>();
            byte[] currentBytes;
            
            //0,1 bytes of the payload
            currentBytes = BitConverter.GetBytes(ADDRESS_thisNode);
            foreach (byte b in currentBytes) payload_list.Add(b);
            
            //2,3 bytes of the payload
            currentBytes = BitConverter.GetBytes(ADDRESS_destinationNode);
            foreach (byte b in currentBytes) payload_list.Add(b);
            
            //4,5,6,7,8,9,10,11 bytes of the payload
            foreach (byte b in timeStamp) payload_list.Add(b);

            foreach (KeyValuePair<char, Node> pair in values)
            {
                //Dont need to tell destination cost from {this} to {this}
                if (pair.Key == ADDRESS_thisNode) continue;

                //Don't tell other node cost from this node to other node. Cost is determined by transportation time if timeCost = true.
                //If timeCost = false, Cost is determined by reading the input file associated with this instance.
                if (pair.Key == ADDRESS_destinationNode) continue;

                //If I route through destination node to get to other node, tell destination node my distance to other node is infinite.
                if (pair.Value.To != ADDRESS_destinationNode && pair.Value.Jump == ADDRESS_destinationNode)
                {
                    currentBytes = BitConverter.GetBytes(pair.Value.To);
                    foreach (byte b in currentBytes) payload_list.Add(b);

                    currentBytes = BitConverter.GetBytes(pair.Value.Jump);
                    foreach (byte b in currentBytes) payload_list.Add(b);

                    currentBytes = costFlag;
                    foreach (byte b in currentBytes) payload_list.Add(b);

                    currentBytes = (BitConverter.GetBytes(infinity));
                    foreach (byte b in currentBytes) payload_list.Add(b);
                    continue;
                }

                //Tell Destination Node the cost to get from this node to pair.Value.To node.
                currentBytes = BitConverter.GetBytes(pair.Value.To);
                foreach (byte b in currentBytes) payload_list.Add(b);

                currentBytes = BitConverter.GetBytes(pair.Value.Jump);
                foreach (byte b in currentBytes) payload_list.Add(b);
                
                currentBytes = costFlag;
                foreach (byte b in currentBytes) payload_list.Add(b);
                
                currentBytes = BitConverter.GetBytes(pair.Value.Cost);
                foreach (byte b in currentBytes) payload_list.Add(b);
            }

            return payload_list.ToArray();
        }

        /// <summary>
        /// Thread which runs until the execution is terminated.
        /// </summary>
        protected override void RoutingServer()
        {
            PrintRoutingTable(distanceVectors, ADDRESS_thisNode);
            timer.Start();
            while (true)
            {
                try
                {
                    receivedBytes = rcv_udpClient.Receive(ref endPoint);
                    InterpretBytes(receivedBytes);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
                Thread.Sleep(15);
            }
        }

        /// <summary>
        /// Payload Layout
        /// Bytes 0 - 1: Address of the node that sent the packet.
        /// Bytes 2 - 3: Address of the destination node.
        /// Bytes 4 - 11: Creation Timestamp of the Packet.
        /// Bytes 12 - length: distance vectors
        /// </summary>
        /// <param name="rcvBytes"></param>
        protected override void InterpretBytes(byte[] rcvBytes)
        {
            receivedPkt = true;
            //The first 8 bits of the packet are the address (a character) of the node that sent this packet.
            receivedNodeAddress = Convert.ToChar(rcvBytes[0]);

            //If we heard our own transmission (all nodes are listening to the same port), return.
            if (receivedNodeAddress == ADDRESS_thisNode)
            {
                return;
            }
            
            //The intended receiving node's address is at the 3rd byte of the received packet.
            char destinationNodeAddress = Convert.ToChar(rcvBytes[2]);

            //If the packet was intended to be received by another node, return.
            //NOTE: We must make this check because this is a simulation running 
            //on one machine and all addresses in the simulation are merely characters.
            if (destinationNodeAddress != ADDRESS_thisNode)
            {
                return;
            }

            //The timestamp of when the received packet was made.
            byte[] rcvTimestamp = new byte[8];
            Array.Copy(rcvBytes, 4, rcvTimestamp, 0, 8);

            //The timestamp of when the packet was received
            timeStamp = BitConverter.GetBytes(DateTime.Now.Ticks);

            double rcvTime = BitConverter.ToDouble(rcvTimestamp, 0);
            double time = BitConverter.ToDouble(timeStamp, 0);
            
            //Time is a double value of which we need the last 4 bytes (first four bytes contain all 0 bits).
            time = time - rcvTime;
            timeStamp = BitConverter.GetBytes(time);
            timeFloat = (timeStamp[4] << 3 | timeStamp[5] << 2 | timeStamp[6] << 1 | timeStamp[7])/100;
            
            //Flag to determine if this is the first packet we've heard from the address cointained in the packet.
            bool firstIntercept = false;

            //If this is a valid address for simulation, decompose payload.
            if (Char.IsLetterOrDigit(receivedNodeAddress))
            {
                //If other node is a node physically attached to this node:
                if (distanceVectors.ContainsKey(receivedNodeAddress))
                {
                    Console.WriteLine("Received broadcast pkt from {0}...", receivedNodeAddress);
                    
                    //If the dictionary of vectors (at this node) associated with address of received node's packet is empty:
                    if (distanceVectors[receivedNodeAddress].Count == 0)
                    {
                        firstIntercept = true;
                    }

                    Node newVector = new Node();
                    newVector.From = receivedNodeAddress;
                    float receivedCost = 0;

                    //Get all vectors included in received packet.
                    for (int i = 12; i < rcvBytes.Length;)
                    {
                        if (rcvBytes[i] == costFlag[0])
                        {
                            i = i + 5;
                            continue;
                        }
                        try
                        {
                            if (rcvBytes[i + 4] == costFlag[0])
                            {
                                //Check to see that is is not a false-positive flag.
                                if (i + 7 < rcvBytes.Length)
                                {
                                    receivedCost = (BitConverter.ToSingle(rcvBytes, i + 5));
                                    newVector.Cost = receivedCost;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                        newVector.To = Convert.ToChar(rcvBytes[i]);
                        
                        //Not used
                        if (i == 0)
                        {
                            newVector.Jump = '\0';
                            newVector.Cost = 0;
                            if (firstIntercept) distanceVectors[receivedNodeAddress].Add(receivedNodeAddress, new Node(newVector));
                            else distanceVectors[receivedNodeAddress][receivedNodeAddress] = new Node(newVector);
                            PrintRoutingTable(distanceVectors, ADDRESS_thisNode);
                            i = i + 2;
                            continue;
                        }

                        newVector.Jump = Convert.ToChar(rcvBytes[i + 2]);

                        //If it's the first time hearing from this node or the node has a new node it can reach:
                        if (firstIntercept || !distanceVectors[receivedNodeAddress].ContainsKey(newVector.To))
                        {
                            distanceVectors[receivedNodeAddress].Add(newVector.To, new Node(newVector));
                        }
                        else
                        {
                            //Overwrite old distance vectors with fresh vectors.
                            distanceVectors[receivedNodeAddress][newVector.To] = new Node(newVector);
                        }

                        i = i + 4;
                    };

                    //Update this node's routing table in the context of newly received packet.
                    UpdateRoutingTable();
                }
                //If this is a node we are not physically attached to:
                else
                {
                    //Receiving a packet from a node not currently seen as directly connected would imply
                    //that we are directly connected to that node. However, since this is a simulation, and
                    //all packets are being sent and received by this machine, if node distances weren't artificially
                    //enforced, it is likely that our table would show every node connected to every other node,
                    //and little valuable information on the correctness of the algorithm would be gained.
                }
            }
        }

        /// <summary>
        /// Updates this node's routing table immediately upon receiving a new packet. Ensures all cost values are minimized.
        /// </summary>
        protected override void UpdateRoutingTable()
        {
            //We need to maintain a table of direct costs to all nodes because it is possible that they become impossible to reach through all connected nodes.
            if (timeCost)
            {
                if (!directCosts.ContainsKey(receivedNodeAddress)) directCosts.Add(receivedNodeAddress, timeFloat);
                else directCosts[receivedNodeAddress] = timeFloat;
            }
            else
            {
                //We need to reload this instance's file to check for changes to its link costs.
                using (StreamReader file = new StreamReader(Directory.GetCurrentDirectory() + "\\" + filename))
                {
                    string line;
                    string[] tokens;

                    while ((line = file.ReadLine()) != null)
                    {
                        tokens = line.Split(' ');
                        if (tokens.Length == 1) continue;
                        if (!directCosts.ContainsKey(tokens[0].ToCharArray()[0])) directCosts.Add(tokens[0].ToCharArray()[0], float.Parse(tokens[1]));
                        else directCosts[tokens[0].ToCharArray()[0]] = float.Parse(tokens[1]);
                    }
                }
            }

            //Print the direct costs of this node -> other directly connected nodes.
            if (true)
            {
                foreach (char c in new List<char>(directCosts.Keys)) Console.WriteLine("Cost to travel directly from {0} to {1}: {2}", ADDRESS_thisNode, c, directCosts[c]);
            }

            //Update distace vector from this node to the node that just sent the packet that triggered this table update.
            if (timeCost)
            {
                if (distanceVectors[ADDRESS_thisNode][receivedNodeAddress].Cost > timeFloat ||
                    (distanceVectors[ADDRESS_thisNode][receivedNodeAddress].Jump == receivedNodeAddress && distanceVectors[ADDRESS_thisNode][receivedNodeAddress].To == receivedNodeAddress))
                {
                    distanceVectors[ADDRESS_thisNode][receivedNodeAddress] = new Node()
                    {
                        From = ADDRESS_thisNode,
                        To = receivedNodeAddress,
                        Jump = receivedNodeAddress,
                        Cost = timeFloat
                    };
                }
            }
            else
            {
                foreach (char c in new List<char>(distanceVectors[ADDRESS_thisNode].Keys))
                {
                    if (directCosts.ContainsKey(c) && (distanceVectors[ADDRESS_thisNode][c].Cost > directCosts[c] ||
                        (distanceVectors[ADDRESS_thisNode][c].Jump == c && distanceVectors[ADDRESS_thisNode][c].To == c)))
                    {
                        distanceVectors[ADDRESS_thisNode][c] = new Node()
                        {
                            From = ADDRESS_thisNode,
                            To = c,
                            Jump = c,
                            Cost = directCosts[c]
                        };
                    }
                }
            }

            //Reset the cost of current routes to reflect the possible change in link costs from this node -> other nodes.
            foreach(char c in new List<char>(distanceVectors[ADDRESS_thisNode].Keys))
            {   
                //If going through a node to get to another node:
                if (distanceVectors[ADDRESS_thisNode][c].To != distanceVectors[ADDRESS_thisNode][c].Jump)
                {
                    char j = distanceVectors[ADDRESS_thisNode][c].Jump;
                    char t = distanceVectors[ADDRESS_thisNode][c].To;
                    if (distanceVectors.ContainsKey(j))
                    {
                        //Updates *current* costs to reflect routing information received by received packtet.
                        distanceVectors[ADDRESS_thisNode][c].Cost = distanceVectors[ADDRESS_thisNode][j].Cost + distanceVectors[j][t].Cost;
                    }
                }
            }

            //Check distance vectors of neighboring nodes; if new node is discovered -> add new node and to our routing table.
            foreach (KeyValuePair<char, Dictionary<char, Node>> row in distanceVectors)
            {
                //We don't update this node's routing table based on its own vectors
                if (row.Key == ADDRESS_thisNode) continue;
                
                foreach (KeyValuePair<char, Node> vector in row.Value)
                {
                    //If we've discovered a new node reachable from a node, add it to the collection of nodes we can reach from this node.
                    if (!distanceVectors[ADDRESS_thisNode].ContainsKey(vector.Key) && vector.Key != ADDRESS_thisNode && distanceVectors[ADDRESS_thisNode].ContainsKey(vector.Value.From))
                    {
                        distanceVectors[ADDRESS_thisNode].Add(vector.Key, new Node()
                        {
                            From = ADDRESS_thisNode,
                            To = vector.Value.To,
                            Jump = vector.Value.From,
                            Cost = vector.Value.Cost + distanceVectors[ADDRESS_thisNode][vector.Value.From].Cost
                        });
                    } 
                }
            }
            
            //Final pass to fully update all routing information for this node.
            //For every node reachable from this node:
            foreach(char c in new List<char>(distanceVectors[ADDRESS_thisNode].Keys))
            {
                //For every node directly connected to this node:
                foreach (KeyValuePair<char, Dictionary<char, Node>> row in distanceVectors)
                {
                    //Don't update this node's routing table based on its own vectors.
                    if (row.Key == ADDRESS_thisNode) continue;

                    //if current node {c} (reachable from {this}) is reachable from {row} node AND
                    //New_Cost({this} -> {row} -> {c}) < Current_Cost({this} -> {c}) 
                    try
                    {
                        if (row.Value.ContainsKey(c) && row.Value[c].Cost != infinity)
                        {
                            if (distanceVectors[ADDRESS_thisNode][row.Key].Cost != infinity &&
                                ((distanceVectors[ADDRESS_thisNode][row.Key].Cost + row.Value[c].Cost) < distanceVectors[ADDRESS_thisNode][c].Cost))
                            {
                                //If New_Cost({this} -> {row} -> {c}) < Current_Cost({this} -> {c}), we change our route.
                                distanceVectors[ADDRESS_thisNode][c] = new Node()
                                {
                                    From = ADDRESS_thisNode,
                                    To = c,
                                    Jump = row.Key,
                                    Cost = distanceVectors[ADDRESS_thisNode][row.Key].Cost + row.Value[c].Cost
                                };
                            }
                        }

                        //if we can no longer route to a node through any other node, route directly to that node (if possible).
                        if (distanceVectors[ADDRESS_thisNode][c].Cost == infinity)
                        {
                            if (directCosts.ContainsKey(c) && (directCosts[c] < distanceVectors[ADDRESS_thisNode][c].Cost) || distanceVectors[ADDRESS_thisNode][c].Cost == infinity)
                            {
                                distanceVectors[ADDRESS_thisNode][c] = new Node()
                                {
                                    From = ADDRESS_thisNode,
                                    To = c,
                                    Jump = c,
                                    Cost = directCosts[c]
                                };
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            if (receivedPkt)
            {
                Console.WriteLine("Output Number: {0}", ++outputCounter);
                PrintRoutingTable(distanceVectors, ADDRESS_thisNode);
            }
            receivedPkt = false;
        }

        /// <summary>
        /// Initializes a new thread to run a server for receiving packets.
        /// </summary>
        public override void StartServer()
        {
            server = new Thread(new ThreadStart(RoutingServer));
            server.Start();
        }

        /// <summary>
        /// Closes and disposes of sockets used during execution.
        /// </summary>
        public void CleanUpInstanceResources()
        {
            snd_udpClient.Close();
            snd_udpClient.Dispose();
            rcv_udpClient.Close();
            rcv_udpClient.Dispose();
        }

        /// <summary>
        /// Called every 15 seconds to broadcast this node's routing table to all other connected nodes.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void TimedBroadcast(object source, ElapsedEventArgs e)
        {
            //Update routing table to reflect any cost changes
            receivedPkt = false;
            UpdateRoutingTable();
            
            //Broadcast Distance Vectors to neighboring nodes.
            Console.WriteLine("Broadcasting Distance Vectors... \n");
            
            //Timestamp experimentation for calculating Cost.
            byte[] byteTime = BitConverter.GetBytes(DateTime.Now.Ticks);

            if (timer != null) timer.Stop();

            //Broadcast to each neighboring node connected to this node.
            foreach (char c in distanceVectors[ADDRESS_thisNode].Keys)
            {
                byte[] payload = ConstructPayload(distanceVectors[ADDRESS_thisNode], c, byteTime);
                snd_udpClient.Send(payload, payload.Length, brdcstEndPoint);
            }
            
            Thread.Sleep(10);
            timer.Start();
        }

        /// <summary>
        /// Called when entering ctrl+c or ctrl+break during execution.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Terminating Execution...");
            if (e.SpecialKey == ConsoleSpecialKey.ControlC || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                CleanUpInstanceResources();
            }
        }
    }
}
