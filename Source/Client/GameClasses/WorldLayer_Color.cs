using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using RimWorld.Planet;
using UnityEngine;
using Verse;

// RimWorld 1.5+ renamed WorldLayer to WorldDrawLayer and refactored planetary rendering.
// This class was an unused experimental stub in OnlineCity and is commented out to avoid ReflectionTypeLoadException.
/*
namespace RimWorldOnlineCity.GameClasses
{
	public class WorldLayer_Color : WorldLayer
	{
		private const int SubdivisionsCount = 4;

		public const float GlowRadius = 8f;

		public override IEnumerable Regenerate()
		{
			foreach (var item in base.Regenerate())
			{
				yield return item;
			}
			SphereGenerator.Generate(4, 108.1f, Vector3.forward, 360f, out var outVerts, out var outIndices);
			LayerSubMesh subMesh = GetSubMesh(WorldMaterials.PlanetGlow);
			subMesh.verts.AddRange(outVerts);
			subMesh.tris.AddRange(outIndices);
			FinalizeMesh(MeshParts.All);
		}
	}
}
*/