﻿using PedSyncer.Model;
using PedSyncer.Utils;
using System;
using System.Collections.Generic;
using System.Timers;

/**
 * The idea of the service-side calculation of the ped-movement is to move the peds to its next
 * navmeshPosition.
 * 
 * So the assumption is, that the ped moves one feet (one unit in the distance of two positions)
 * every second. If the next navmeshPosition is in a distance of around 4 feet, the server will
 * move this ped to the next navmeshPosition after 4 seconds.
 * 
 * The variable pedMovements stores the peds to move by the seconds till the peds have to move 
 * in seconds.
 * 
 * Example: If ped A has a distance of 2 feet to the next navmeshPosition, it will stored in
 * pedMovements[2]. After one second, it will be in pedMovements[1]. After the next second, it
 * will be in pedMovements[0] and then will be moved to the next navmeshPositions - then start
 * again.
 * 
 * It will now be called as pedMovments-queue
 */

namespace PedSyncer.Control
{
    internal class PedMovement
    {
        private static PedMovement? Instance = null;

        //Start the movement controller with setting up the calculation interval
        private PedMovement()
        {
            Timer Timer = new Timer();
            Timer.Interval = TimeSpan.FromSeconds(1).TotalMilliseconds;
            Timer.AutoReset = true;
            Timer.Elapsed += new ElapsedEventHandler(MovePeds);
            Timer.Enabled = true;
            Timer.Start();
        }

        //Singleton
        public static PedMovement GetInstance()
        {
            if (Instance == null) Instance = new PedMovement();
            return Instance;
        }

        /**
        * Activate the server-side path calculation for a given ped
        * 
        * @param ped Ped which path should be calculated by the server
        */
        public void AddPedMovementCalculcation(Ped ped, bool SetCurrentNavmashPositionsIndex = true)
        {
            if (ped.Freeze || ped.Dead || !ped.Wandering) return;
            if (SetCurrentNavmashPositionsIndex) ped.CurrentNavmashPositionsIndex = GetNearestNavMeshOfPed(ped);

            if (ped.CurrentNavmashPositionsIndex < 0 || ped.PathPositions.Count >= ped.CurrentNavmashPositionsIndex)
            {
                ped.Wandering = true;
                return;
            }

            ped.Position = ped.PathPositions[ped.CurrentNavmashPositionsIndex].Position;

            if (ped.PathPositions.Count < ped.CurrentNavmashPositionsIndex + 1)
            {
                ped.ContinueWandering();
                ped.CurrentNavmashPositionsIndex = 0;
            }

            AddPedMovement(
                (int)Math.Ceiling(Vector3Utils.GetDistanceBetweenPos(ped.Position, ped.PathPositions[ped.CurrentNavmashPositionsIndex + 1].Position)),
                ped
            );
        }

        private Dictionary<int, List<Ped>> PedMovements = new Dictionary<int, List<Ped>>();

        //Method to add the ped to the pedMovments-queue
        private void AddPedMovement(int Distance, Ped Ped)
        {
            if (!PedMovements.ContainsKey(Distance)) PedMovements.Add(Distance, new List<Ped>());
            PedMovements[Distance].Add(Ped);
        }

        /**
         * Return the peds which have to be moved now (index 0) and decrease all keys by one
         */
        private List<Ped> PopPedMovements()
        {
            //Store peds to return
            List<Ped> ToReturn = new List<Ped>();
            if (PedMovements.ContainsKey(0)) ToReturn = PedMovements[0];

            //Decrease all other keys than 0 by 1
            Dictionary<int, List<Ped>> NewPedMovements = new Dictionary<int, List<Ped>>();
            foreach (int PedMovementsKey in PedMovements.Keys)
            {
                if (PedMovementsKey == 0) continue;
                NewPedMovements[PedMovementsKey - 1] = PedMovements[PedMovementsKey];
            }

            PedMovements = NewPedMovements;

            return ToReturn;
        }

        /**
         * Move peds in pedMovement-queue at key 0 to their next navmeshPosition
         */
        private void MovePeds(object sender, ElapsedEventArgs e)
        {
            //Pop the peds at key 0 and decrease all other keys by 1
            List<Ped> PedsToMove = PopPedMovements();

            if (PedsToMove.Count == 0) return;

            foreach (Ped Ped in PedsToMove)
            {
                //If the ped now has a netOwner: Continue with the next ped, current position is now calculated by the netOwner
                if (Ped.NetOwner != null) continue;

                //Set ped to the next navmeshPosition
                Ped.CurrentNavmashPositionsIndex = Ped.CurrentNavmashPositionsIndex + 1;
                Ped.Position = Ped.PathPositions[Ped.CurrentNavmashPositionsIndex].Position;
                AddPedMovementCalculcation(Ped, false);
            }
        }

        //Function to determine the nearest navMesh to the ped
        public static int GetNearestNavMeshOfPed(Ped Ped)
        {
            if (Ped.PathPositions.Count == 0) return -1;

            int MinimumPos = -1;
            double MinimumDistance = 1000;

            int i = 0;
            foreach (IPathElement NavMesh in Ped.PathPositions)
            {
                double Distance = Vector3Utils.GetDistanceBetweenPos(Ped.Position, NavMesh.Position);

                if (MinimumDistance > Distance)
                {
                    MinimumPos = i;
                    MinimumDistance = Distance;
                }
                i++;
            }

            return MinimumPos;
        }
    }
}