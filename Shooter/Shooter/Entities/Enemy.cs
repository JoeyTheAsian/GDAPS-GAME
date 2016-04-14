﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Shooter.MapClasses;
using Shooter.Other_tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Shooter.Entities {
    class Enemy : Character {
        //Enemy unique data values
        //how far the enemy can see
        private int visionRange;
        //how far the enemy can hear gunshots
        private int scanRange;
        //the size of the area that the enemy will check for a path
        private int pathRange;
        private bool isActing;
        //movement speed
        private double speed;
        //the arc (degrees) which the character can turn to scan
        private double scanArc;
        //the speed (in degrees) that the entity scans for enemies
        private double scanSpeed;
        //queue that holds the coordinates the npc is scheduled to move to
        private Queue<Coord> moveQueue = new Queue<Coord>();
        //Default constructor for normal enemies
        public Enemy(ContentManager content, double x, double y, string t, Rectangle r) : base(content, x, y, t, r) {
            //try to set texture to specified name
            try {
                entTexture = content.Load<Texture2D>(t);
            } catch (FileNotFoundException) {
                entTexture = content.Load<Texture2D>("NoTexture");
                Console.WriteLine(t + "Not found. Using default texture.");
            }

            //collidable object by default
            collision = true;

            //Set health
            health = 4;
            maxHealth = health;
            //set AI scan range
            visionRange = 5;
            scanRange = 5;
            pathRange = 30;
            //tiles per second
            speed = 5;
            isActing = false;
        }
        public bool Move(double elapsedTime, Coord end) {
            if (loc.X > end.X - .05 && loc.X < end.X + .05 && loc.Y > end.Y - .05 && loc.Y < end.Y + .05) {
                return true;
            } else {
                Coord start = loc;
                double x = end.X - loc.X;
                double y = end.Y - loc.Y;
                double h = Math.Sqrt(x * x + y * y);
                double distance = speed * (elapsedTime / 1000);
                loc.X += distance * x / h;
                loc.Y += distance * y / h;
                //account for overshoot
                if (start.X < end.X) {
                    if (loc.X > end.X)
                        loc.X = end.X;
                }
                if (start.X > end.X) {
                    if (loc.X < end.X)
                        loc.X = end.X;
                }
                if (start.Y > end.Y) {
                    if (loc.Y < end.Y)
                        loc.Y = end.Y;
                }
                if (start.Y < end.Y) {
                    if (loc.Y > end.Y)
                        loc.Y = end.Y;
                }
                if (loc.X > end.X - .1 && loc.X < end.X + .1 && loc.Y > end.Y - .1 && loc.Y < end.Y + .1) {
                    return true;
                }
                //if the coordinates are close enough, return true to indicate that moving was successful
                else {
                    return false;
                }
            }
        }
        public void UpdateAI(ref Map m, double elapsedTime) {
            if (moveQueue.Count == 0) {
                if (m.sounds.Count > 0) {
                    //find a path to the sound and put it on move queue
                    List<Coord> path = GetPath(loc, m.sounds[m.sounds.Count - 1], ref m);
                    foreach (Coord c in path) {
                        moveQueue.Enqueue(c);
                    }
                }
            } else {
                if (Move(elapsedTime, moveQueue.Peek())) {
                    moveQueue.Dequeue();
                }
            }

        }
        //Checks all adjacent tiles and returns list of valid tiles sorted by estimated path length
        public List<Node> checkAdjacent(Node curNode, Coord end, ref Node[,] nodeMap) {
            int X = (int)curNode.location.X;
            int Y = (int)curNode.location.Y;

            List<Node> validNodes = new List<Node>();

            //check for valid adjacent tiles and add them to list
            for (int i = X - 1; i < X + 2; i++) {
                for (int j = Y - 1; j < Y + 2; j++) {
                    //stay within boundaries of map and check if the tile is walkable
                    if (i > 0 && j > 0 && nodeMap[i,j].collidable == false) {
                        Node n = nodeMap[i, j];
                        // Already-open nodes are added to the list only if you find a shorter way to get there
                        if (n.state == Node.NodeState.Open) {
                            //if the currently stored path to that node is longer, override with shorter path
                            if(n.pathLength > n.GetNewPathDist(curNode)) {
                                n.parent = curNode;
                                n.UpdateDist(n.location, end);
                                validNodes.Add(n);
                            }
                        } 
                        if (n.state == Node.NodeState.Closed) {
                            //do nothing if the node is closed
                            continue;
                        }
                        //untested node case
                        else {
                            n.parent = curNode;
                            n.state = Node.NodeState.Open;
                            n.UpdateDist(n.location, end);
                            validNodes.Add(n);
                        }
                        //update node on map
                        nodeMap[i,j] = n;
                    }
                }
            }
            //sort the nodes by the least distance first
            validNodes = validNodes.OrderBy(o => o.estLength).ToList();
            return validNodes;
        }
        
        public bool Search(Node curNode, Coord startPos, Coord endPos,ref Node[,] nodeMap) {
            //all nodes added to a path are marked as closed
            curNode.state = Node.NodeState.Closed;
            //get sorted list of valid moves
            List<Node> validNodes = checkAdjacent(curNode, endPos, ref nodeMap);
            
            foreach (var nextNode in validNodes) {
                //if the next node you check is the end, return true
                if ((int)nextNode.location.X == (int)endPos.X && (int)nextNode.location.Y == (int)endPos.Y) {
                    return true;
                } else {
                    //recurse until dead end is met or end is reached
                    if (Search(nextNode, startPos, endPos, ref nodeMap)) {
                        return true;
                    }
                }
            }
            //if all paths are checked and cannot reach destination, return false
            return false;
        }
        //compiles the path
        public List<Coord> GetPath(Coord startPos, Coord endPos, ref Map m) {
            //round down coords to lock to tiles
            int startX = (int)(startPos.X);
            int startY = (int)(startPos.Y);
            int endX = (int)(endPos.X);
            int endY = (int)(endPos.Y);
            //create nodemap
            Node[,] nodeMap = new Node[m.ObjectMap.GetLength(0), m.ObjectMap.GetLength(1)];
            for (int x = 0; x < nodeMap.GetLength(0); x++) {
                for (int y = 0; y < nodeMap.GetLength(1); y++) {
                    if (m.ObjectMap[x, y] != null) {
                        nodeMap[x, y] = new Node(new Coord(x, y), m.ObjectMap[x, y].collision, new Coord(endX, endY), null);
                    } else {
                        nodeMap[x, y] = new Node(new Coord(x, y), false, new Coord(endX, endY), null);
                    }
                }
            }

            bool success = Search(nodeMap[startX, startY], new Coord(startX, startY), new Coord(endX, endY), ref nodeMap);
            if (success) {
                List<Coord> path = new List<Coord>();
                Node n = nodeMap[endX, endY];
                while (n.parent != null) {
                    path.Add(new Coord(n.location.X, n.location.Y));
                    n = n.parent;
                }
                path.Reverse();
                return path;
            } else {
                //return empty list if search is not successful
                return new List<Coord>();
            }
        }
    }
}
