﻿#region Using
using OTAPI.Tile;
using System;
#endregion
namespace FakeProvider
{
    public interface INamedTileCollection : ITileCollection, IDisposable
    {
        IProviderTile this[int x, int y] { get; set; }

        string Name { get; }
        int X { get; set; }
        int Y { get; set; }
        int Layer { get; }
        bool Enabled { get; }

        void Draw(bool section);
    }
}