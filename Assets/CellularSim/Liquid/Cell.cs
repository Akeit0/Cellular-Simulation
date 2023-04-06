using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
namespace CellularSim.Liquid {
    public enum CellType :byte{
        AirOrWater=0,
        Solid,
        Hole,
    }
    public struct Cell {
        public CellType CellType;
        public byte Option;
        public short Liquid;

       
        public Cell(CellType cellType,byte option, short liquid) {
            CellType = cellType;
            Option = option;
            Liquid = liquid;
        }

        public static Cell Solid() => new Cell(CellType.Solid, 0, 0);
        public static Cell Hole() => new Cell(CellType.Hole, 0, 0);
        public static Cell Source(short liquid) => new Cell(CellType.Solid, 0, liquid);
       
        public bool IsAirOrWater => CellType == 0;
        public bool IsSolid=> CellType== CellType.Solid;
    }

     public struct Diffs {
        public Cell Next;
        public short Bottom;
        public short Top;
        public short Left;
        public short Right;
    }
    
    public struct DiffsUD {
        public Cell Next;
        public short Bottom;
        public short Top;
        
    }
    
    
    
}