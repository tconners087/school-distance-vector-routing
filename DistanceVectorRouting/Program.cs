using System;
using System.Collections.Generic;
using System.IO;

//AUTHOR: Taylor Conners
namespace DistanceVectorRouting
{
    class Program
    {
        //'vectors' Dictionary will be the value of the 'table' Dictionary:
        private static Dictionary<char, Node> vectors;
        private static Dictionary<char, Dictionary<char, Node>> table;
        private static int numNeighbors;

        static void Main(string[] args)
        {
            DistanceRouter_Simulation router;
            vectors = new Dictionary<char, Node>();
            table = new Dictionary<char, Dictionary<char, Node>>();
            
            int port = 0;

            try
            {
                char from = args[0].ToString().Split('.')[0][0];

                using (StreamReader file = new StreamReader(Directory.GetCurrentDirectory() + "\\" + args[0]))
                {
                    string line;
                    string[] tokens;

                    while ((line = file.ReadLine()) != null)
                    {
                        tokens = line.Split(' ');
                        if (tokens.Length == 1) numNeighbors = int.Parse(tokens[0]);
                        else if (tokens.Length == 2)
                        {
                            Node newNeighbor = new Node
                            {
                                From = from, 
                                To = tokens[0][0],
                                Jump = tokens[0][0],
                                Cost = float.Parse(tokens[1])
                            };
                            vectors.Add(newNeighbor.To, newNeighbor);
                        }
                        else
                        {
                            Console.WriteLine("File provided is incorrectly formatted. Please ensure file is valid.");
                            throw new ArgumentException();
                        }
                    }
                }

                /*
                 * At Key = {from}, we maintain:
                 * (1) the cost to reach each node 'n' E N where N == all routers reachable from {this} router.
                 * (2) the next-hop node to reach node 'n' from node {from}
                 * 
                 * For routing table at {this} router, we maintain:
                 * (1) The distance vectors of each neighbor 'v' of {this} router s.t. D_v = [D_v(y) | y E N]
                 * (2) The physical neighbors of {this} router are the ones included in the file supplied at the start of the application.
                 * 
                 * */

                table.Add(from, vectors);
                foreach(KeyValuePair<char, Node> vector in table[from])
                {
                    if (!table.ContainsKey(vector.Key)) table.Add(vector.Key, new Dictionary<char, Node>());
                }
                
                try
                {
                    if (args.Length > 1)
                    {
                        port = int.Parse(args[1]);
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                router = new DistanceRouter_Simulation(table, numNeighbors, from, args[0].ToString(), port);
                router.StartServer();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Prints a routing table. Deprecated to a similar method in DistanceRouter.cs
        /// </summary>
        /// <param name="routingTable"></param>
        public static void PrintRoutingTable(Dictionary<char, Dictionary<char,Node>> routingTable)
        {
            foreach(KeyValuePair<char, Dictionary<char,Node>> row in routingTable)
            {
                Console.Write("FROM {0}: ", row.Key);
                if (row.Value != null && row.Value.Count != 0)
                {
                    foreach (KeyValuePair<char, Node> vector in row.Value)
                    {
                        Console.Write("<TO: {0}, NEXT HOP NODE: {1}, COST: {2}> ", vector.Value.To, vector.Value.Jump, vector.Value.Cost);
                    }
                }
                else
                {
                    Console.Write("Distance Vectors for Node {0} are unknown.", row.Key);
                }
                Console.WriteLine();
            }
        }
        
        //Not currently used. 
        static void CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Terminating Execution...");
            if (e.SpecialKey == ConsoleSpecialKey.ControlC || e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                //router.CleanUpInstanceResources();
            }
        }
    }

    /// <summary>
    /// Stores all relevant information to be used in determining the shortest path from one node to another.
    /// </summary>
    public class Node
    {
        public char From { get; set; }
        public char To { get; set; }
        public char Jump { get; set; }
        public float Cost { get; set; }

        public Node() { }

        public Node(Node other)
        {
            this.From = other.From;
            this.To = other.To;
            this.Jump = other.Jump;
            this.Cost = other.Cost;
        }
    }
}
