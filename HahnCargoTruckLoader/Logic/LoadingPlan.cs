using System;
using System.Collections.Generic;
using System.Linq;
using HahnCargoTruckLoader.Model;

namespace HahnCargoTruckLoader.Logic
{
    public class LoadingPlan
    {
        private readonly Dictionary<int, LoadingInstruction> instructions;
        private readonly Truck _truck;
        private readonly List<Crate> _crates;

        public LoadingPlan(Truck truck, List<Crate> crates)
        {
            instructions = new Dictionary<int, LoadingInstruction>();
            _truck = truck;
            _crates = crates;
        }

        public Dictionary<int, LoadingInstruction> GetLoadingInstructions()
        {
            try
            {
                // Calculate volumes
                int totalCrateVolume = _crates.Sum(c => c.Width * c.Height * c.Length);
                int truckVolume = _truck.Width * _truck.Height * _truck.Length;

                // Early exit if total volume of crates exceeds truck volume
                if (totalCrateVolume > truckVolume)
                {
                    throw new Exception($"The total volume of the crates ({totalCrateVolume}) exceeds the truck's cargo capacity ({truckVolume}).");
                }

                // Sort crates by volume in descending order
                var sortedCrates = _crates.OrderByDescending(c => c.Width * c.Height * c.Length).ToList();

                // 3D boolean array to track busy space in the truck
                bool[,,] truckSpace = new bool[_truck.Width, _truck.Height, _truck.Length];
                int step = 1;

                foreach (var crate in sortedCrates)
                {
                    if (!PlaceCrateInTruck(crate, truckSpace, ref step))
                    {
                        throw new CratePlacementException(crate.CrateID, $"Failed to place crate {crate.CrateID}");
                    }
                }
            }
            catch (CratePlacementException ex)
            {
                Console.WriteLine($"Error: Crate ID {ex.CrateId} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return instructions;
        }

        private bool PlaceCrateInTruck(Crate crate, bool[,,] cargoSpace, ref int step)
        {
            foreach (var (width, height, length) in GetPossibleCrateRotations(crate))
            {
                for (int x = 0; x <= _truck.Width - width; x++)
                {
                    for (int y = 0; y <= _truck.Height - height; y++)
                    {
                        for (int z = 0; z <= _truck.Length - length; z++)
                        {
                            if (CanPlace(cargoSpace, x, y, z, width, height, length))
                            {
                                PlaceCrate(cargoSpace, x, y, z, width, height, length);
                                CreateAndStoreInstruction(crate, x, y, z, width, height, length, ref step);
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private bool CanPlace(bool[,,] space, int x, int y, int z, int width, int height, int length, double supportThreshold = 0.75)
        {
            // Check if there is sufficient support beneath this crate
            if (y > 0)
            {
                int totalBaseCells = width * length;
                int requiredSupportCells = (int)Math.Ceiling(totalBaseCells * supportThreshold); // Calculate the minimum required support cells based on the threshold
                int supportedCells = 0;

                for (int i = x; i < x + width; i++)
                {
                    for (int k = z; k < z + length; k++)
                    {
                        if (space[i, y - 1, k])
                        {
                            supportedCells++;
                        }
                    }
                }

                if (supportedCells < requiredSupportCells)
                {
                    return false; // Not enough support underneath
                }
            }

            // Check if the space is free
            for (int i = x; i < x + width; i++)
            {
                for (int j = y; j < y + height; j++)
                {
                    for (int k = z; k < z + length; k++)
                    {
                        if (space[i, j, k])
                        {
                            return false; // Space is already busy
                        }
                    }
                }
            }
            return true;
        }



        private void PlaceCrate(bool[,,] space, int x, int y, int z, int width, int height, int length)
        {
            for (int i = x; i < x + width; i++)
            {
                for (int j = y; j < y + height; j++)
                {
                    for (int k = z; k < z + length; k++)
                    {
                        space[i, j, k] = true;
                    }
                }
            }
        }

        private void CreateAndStoreInstruction(Crate crate, int x, int y, int z, int width, int height, int length, ref int step)
        {
            bool turnHorizontal = false;
            bool turnVertical = false;

            // Determine if the crate has been rotated horizontally or vertically
            if (width != crate.Width || height != crate.Height || length != crate.Length)
            {
                if (width == crate.Length && height == crate.Height && length == crate.Width)
                {
                    turnHorizontal = true;
                }
                else if (width == crate.Width && height == crate.Length && length == crate.Height)
                {
                    turnVertical = true;
                }
            }

            var instruction = new LoadingInstruction
            {
                LoadingStepNumber = step++,
                CrateId = crate.CrateID,
                TopLeftX = x,
                TopLeftY = y,
                TurnHorizontal = turnHorizontal,
                TurnVertical = turnVertical
            };

            instructions[crate.CrateID] = instruction;
            //PrintInstruction(instruction);
        }

        private IEnumerable<(int, int, int)> GetPossibleCrateRotations(Crate crate)
        {
            return new List<(int, int, int)>
            {
                (crate.Width, crate.Height, crate.Length),
                (crate.Length, crate.Height, crate.Width),
                (crate.Width, crate.Length, crate.Height)
            };
        }

        private void PrintInstruction(LoadingInstruction instruction)
        {
            Console.WriteLine($"Step {instruction.LoadingStepNumber}: " +
                              $"Crate {instruction.CrateId} at ({instruction.TopLeftX}, {instruction.TopLeftY}) " +
                              $"{(instruction.TurnHorizontal ? "turned horizontally" : "")} " +
                              $"{(instruction.TurnVertical ? "turned vertically" : "")}");
        }

    }

    // Custom exception for crate placement failures
    public class CratePlacementException : Exception
    {
        public int CrateId { get; }

        public CratePlacementException(int crateId, string message) : base(message)
        {
            CrateId = crateId;
        }
    }

}
