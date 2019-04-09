# school-distance-vector-routing

ITCS - 6166: Project 03

Author: Taylor Conners

## Explanation

This is a simulation of the Distance Vector routing protocol designed to run on one machine, but it will work across multiple computers, provided all of them are free to listen to a specified port, all of them are  up-to-date with Microsoft's .NET core, and all of them follow the input file specifications.

## To Run

**USAGE:** dotnet run <file_path> <port_num>

This project requires .NET core, which can be installed from: https://www.microsoft.com/net/learn/get-started/windows

1. Navigate your Command Prompt to the directory containing the *DistanceVectorRouting.csproj* project file. (e.g.'...\repos\DistanceVectorRouting\DistanceVectorRouting')

2. For each file (for as many nodes you wish to simulate the protocol with) type "dotnet run filename port" where 'filename' is the name of the file following the parameters outlined in the project document and 'port' is an integer value of a UDP port that the program will use when opening a socket. If no port is specified, the program will open a socket on port #52.

    * dotnet run A.txt 52

3. To test the routing protocol while the simulations are running, open one of the files containing the initial routing information (connected nodes and their costs) and edit the cost of a neighboring node. When that node broadcasts its distance vectors, all other nodes connected will adjust their routing tables accordingly upon receiving the packet.

4. To end the simulation, enter "ctrl+c" or "ctrl+break" in the Command Prompt window of eachinstance.

## Input File Format

The project requires the input file to follow the specifications included in the project document identically, with the following stipulations:

1. The name of a node must be a single character or digit. It cannot be more than one character in length or else the algorithm will fail to compose and decompose packets correctly.

2. The name of the node cannot be a lowercase 'f'. I am using this character's byte value to serve as a flag to indicate an incoming 'float' value in a packet. That is, when reading a packet, the program knows that an 'f' byte means the following 4 bytes are a floating point cost value. Naming a node 'f' may cause behavior to trigger which will invalidate the routing table until the simulation is closed and restarted.

* Sample Input File Format:

```txt
3
A 4.2
B 2.3
C 1.0
```

## Other Notes

**NOTE**: There is another way to run the simulation, which I developed for testing the correctness of the algorithm which bases the cost of traveling from node A -> node B by the time it takes a packet to travel from node B -> node A. That is, when node A receives a packet from node B, it sets the cost to travel from itself to node B to the time elapsed during the transmission of B's packet. This results in very volatile and shifting costs, but it shows how the algorithm handles routing in a turbulent environment. To enable this behavior, navigate to line 67 of  DistancRouter_Simulation.cs and set the boolean variable "timeCost = true". If this is the case, no manual editing of the input file is needed to test the algorithm.