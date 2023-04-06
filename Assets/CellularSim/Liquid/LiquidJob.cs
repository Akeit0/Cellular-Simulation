using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace CellularSim.Liquid {
    [BurstCompile]
    public struct CalculateWaterPhysics : IJobParallelFor {
        //Calculate water physics and then save them in the next array
        [ReadOnly] public NativeArray<Cell> Cells;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<DiffsUD> Diffs;

        [ReadOnly] public int MaxLiquid;
        [ReadOnly] public int MinLiquid;
        [ReadOnly] public int Compression;
        [ReadOnly] public float FlowSpeed;
        [ReadOnly] public int HorizontalFlowFactor;
        [ReadOnly] public int GridHeight;
        [ReadOnly] public int GridWidth;

        public void Execute(int index) {
            var offset = index * GridWidth;
            var first = CalcDiff(offset);
            var firstNext = first.Next;
            var second = CalcDiff(offset + 1);

            firstNext.Liquid += second.Left;

            Diffs[offset] = new DiffsUD()
                {Next = firstNext, Top = first.Top, Bottom = first.Bottom};

            for (int x = 2; x < GridWidth; x++) {
                var d = CalcDiff(x + offset);
                var secondNext = second.Next;
                secondNext.Liquid += (short) (first.Right + d.Left);
                Diffs[offset + x - 1] = new DiffsUD()
                    {Next = secondNext, Top = second.Top, Bottom = second.Bottom};
                first = second;
                second = d;
            }

            {
                var secondNext = second.Next;
                secondNext.Liquid += (first.Right);
                Diffs[offset + GridWidth - 1] = new DiffsUD()
                    {Next = secondNext, Top = second.Top, Bottom = second.Bottom};
            }
        }

        public Diffs CalcDiff(int index) {
            // Validate cell
            var CellsCell = Cells[index];
            var diff = default(Diffs);
            diff.Next = CellsCell;
            var remainingLiquid = CellsCell.Liquid;
            if (remainingLiquid == 0) {
                return diff;
            }

            
            // Flow to bottom cell
            if ((GridWidth <= index)) //Has bottom neighbor
            {
                var bottomCell = Cells[index - GridWidth];
                if (!bottomCell.IsSolid ) 
                {
                    // Determine rate of flow
                    var bottomLiquid = bottomCell.Liquid;
                    var flow = CalculateVerticalFlowValue(remainingLiquid, bottomLiquid) - bottomLiquid;
                    if (0 < flow) {
                        if (bottomLiquid > 0) flow = (int) (flow * FlowSpeed);
                        var sFlow = (short) Min(flow, remainingLiquid);
                        diff.Bottom += sFlow;
                        if (CellsCell.IsAirOrWater) {
                            remainingLiquid -= sFlow;
                            diff.Next.Liquid = remainingLiquid;
                            if (remainingLiquid < MinLiquid) return diff;
                        }
                    }
                }
            }
            
            
            if ((index + GridWidth < GridHeight * GridWidth)) {
                var topCell = Cells[index + GridWidth];
                if (!topCell.IsSolid) {
                    var flow = remainingLiquid - CalculateVerticalFlowValue(remainingLiquid, topCell.Liquid);
                    // Adjust temp values
                    if (0 < flow) {
                        flow  =Min(flow,remainingLiquid);
                        var sFlow = (short)  (flow * FlowSpeed);
                        diff.Top += sFlow;
                        if (CellsCell.IsAirOrWater) {
                            remainingLiquid -= sFlow;
                            diff.Next.Liquid = remainingLiquid;
                            if (remainingLiquid < MinLiquid) return diff;
                        }
                    }
                }
            }
            
            var leftCell = (index) % GridWidth == 0 ? Cell.Solid() : Cells[index - 1];
            var rightCell = (index + 1) % GridWidth == 0 ? Cell.Solid() : Cells[index + 1];
            var remainLR = remainingLiquid;
            if (!leftCell.IsSolid)  {
                // Calculate flow rate
                var flow = (remainingLiquid - leftCell.Liquid) / 4;

                // Adjust temp values
                if (0 < flow) {
                    if ((rightCell.CellType == CellType.Solid || remainingLiquid < rightCell.Liquid)) {
                        flow *= HorizontalFlowFactor;
                    }

                    if (MinLiquid <= flow) flow = Min((int) (flow * FlowSpeed), remainingLiquid);
                    var sFlow = (short) flow;
                    diff.Left += sFlow;
                    if (CellsCell.IsAirOrWater) {
                        remainingLiquid -= sFlow;
                        diff.Next.Liquid = remainingLiquid;
                        if (remainingLiquid < MinLiquid) return diff;
                    }
                }
            }

            //Flow to Right
            if (!rightCell.IsSolid)  {
                // Adjust temp values
                var flow = remainingLiquid - rightCell.Liquid;
                if (0< flow) {
                    if (leftCell.CellType == CellType.Solid || remainLR < leftCell.Liquid) {
                        flow =flow*HorizontalFlowFactor /4;
                    }
                    else {
                        flow  /=3;
                    }
                    if (MinLiquid <= flow) flow = Min((int) (flow * FlowSpeed), remainingLiquid);
                    var sFlow = (short) flow;
                    diff.Right += sFlow;
                    if (CellsCell.IsAirOrWater) {
                        remainingLiquid -= sFlow;
                        diff.Next.Liquid = remainingLiquid;
                        if (remainingLiquid < MinLiquid) return diff;
                    }
                }
            }
        
          
            return diff;
        }

        static int Min(int left, int right) => Math.Min(left, right);


        int CalculateVerticalFlowValue(int remainingLiquid, int destination) {
            int sum = remainingLiquid + destination;
            if (sum <= MaxLiquid)
                return MaxLiquid;
            if (sum < 2 * MaxLiquid + Compression)
                return (MaxLiquid * MaxLiquid + sum * Compression) / (MaxLiquid + Compression);
            return (sum + Compression) / 2;
        }
    }

    [BurstCompile]
    public struct ApplyWaterPhysics : IJobParallelFor {
        [ReadOnly] public NativeArray<DiffsUD> Diffs;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<Cell> Cells;

        [ReadOnly] public int MinLiquid;
        [ReadOnly] public int GridWidth;
        [ReadOnly] public int GridHeight;

        public void Execute(int y) {
            var offset = y * GridWidth;
            for (int i = 0; i < GridWidth; i++) {
                var index = i + offset;
                var CellsDiffs = Diffs[index];
                var CellsCell = CellsDiffs.Next;
                if (CellsCell.CellType != 0) {
                    continue;
                } 

                if (y != GridHeight - 1) CellsCell.Liquid += Diffs[index + GridWidth].Bottom;
                if (y != 0) CellsCell.Liquid += Diffs[index - GridWidth].Top;

                if (CellsCell.Liquid < MinLiquid) {
                    Cells[index] = default;
                }
                else {
                    Cells[index] = CellsCell;
                }
            }
        }
    }
}