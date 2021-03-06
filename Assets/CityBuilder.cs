using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using Delaunay;
using Delaunay.Geo;
using Delaunay.LR;
using UnityEngine.AI;
using Random = UnityEngine.Random;

public class CityBuilder : MonoBehaviour
{
	public List<Car> carPrefabs = new List<Car>();
	public List<Car> bikePrefabs = new List<Car>();
	public List<GameObject> roadPrefabs = new List<GameObject>();
	public List<GameObject> bridgePrefabs = new List<GameObject>();
	public List<GameObject> bikeRoadPrefabs = new List<GameObject>();
	public List<GameObject> roadIntersectionPrefabs = new List<GameObject>();
	public List<GameObject> bikeIntersectionPrefabs = new List<GameObject>();
	public List<Building> parkPrefabs = new List<Building>();
	public List<Building> buildingPrefabs = new List<Building>();
	public List<Building> treePrefabs = new List<Building>();
	public List<Building> museumPrefabs = new List<Building>();
	public List<Building> cityHallPrefabs = new List<Building>();
	public List<Building> prisonPrefabs = new List<Building>();
	public Ambulance ambulancePrefab;
	public Police policePrefab;
	public GameObject riverPrefab;
	public Material land;

	public int parkCount = 1000;
	public int buildingCount = 1000;
	public int treeCount = 1000;
	public int museumCount = 1000;
	public int cityHallCount = 1000;
	public int prisonCount = 1000;
	public int pointCount = 1000;
	public int bikePointCount = 1000;
    public int width = 400;
    public int height = 400;
	public float[,] map;

	private Ambulance ambulance = null;
	private Police police = null;
	private Texture2D tx;

	void Start()
    {
		int seed = Random.Range(int.MinValue, int.MaxValue);
		Debug.Log("Random.seed " + seed);
		Random.InitState(seed);

		StartCoroutine(GenerateMap());
    }

	private IEnumerator GenerateMap()
	{
        map = CreateMap();
        Color[] pixels = CreatePixelMap(map);

		GenerateRiver(map, pixels);

		yield return null; // Skip a frame to update river collisions

		/* Generate Graphs */
		List<LineSegment> graph_edges;
		List<Vector2> intersections;
		GenerateGraph(pointCount, x => x, 1.3f, true, out graph_edges, out intersections);

		/* Shows Voronoi diagram */
		foreach (LineSegment seg in graph_edges)
		{
			Vector2 left = (Vector2)seg.p0;
			Vector2 right = (Vector2)seg.p1;
			//DrawLine (pixels,left, right,Color.blue);
			CreateRoad(new Vector3(left.x, 0, left.y), new Vector3(right.x, 0, right.y), roadPrefabs, carPrefabs, bridgePrefabs);
		}

		foreach (Vector2 pos in intersections)
		{
			GameObject prefab = roadIntersectionPrefabs[Random.Range(0, roadIntersectionPrefabs.Count)];
			Instantiate(prefab, transform.position + new Vector3(pos.x, 0, pos.y), Quaternion.identity, transform);
		}

		List<LineSegment> bike_edges;
		List<Vector2> bike_intersections;
		GenerateGraph(bikePointCount, x => 1 - 2 * Math.Abs(x - 0.5f), 0.45f, false, out bike_edges, out bike_intersections);

		/* Shows Voronoi diagram */
		foreach (LineSegment seg in bike_edges)
		{
			Vector2 left = (Vector2)seg.p0;
			Vector2 right = (Vector2)seg.p1;
			//DrawLine (pixels,left, right,Color.blue);
			CreateRoad(new Vector3(left.x, 0, left.y), new Vector3(right.x, 0, right.y), bikeRoadPrefabs, bikePrefabs, new List<GameObject>());
		}

		foreach (Vector2 pos in bike_intersections)
		{
			GameObject prefab = bikeIntersectionPrefabs[Random.Range(0, bikeIntersectionPrefabs.Count)];
			Instantiate(prefab, transform.position + new Vector3(pos.x, 0, pos.y), Quaternion.identity, transform);
		}

		/* Apply pixels to texture */
		tx = new Texture2D(width, height);
		land.SetTexture("_MainTex", tx);
		tx.SetPixels(pixels);
		tx.Apply();

		foreach (NavMeshSurface navSirface in GetComponents<NavMeshSurface>())
		{
			navSirface.BuildNavMesh();
		}

		yield return null; // Skip a frame to update road collisions

		GenerateBuildings(map, parkPrefabs, parkCount, x => 1 - 2 * Math.Abs(x - 0.5f), x => 1.0f);
		GenerateBuildings(map, prisonPrefabs, prisonCount, x => x > 0.55f ? x : 0.0f, x => 1.0f);
		GenerateBuildings(map, cityHallPrefabs, cityHallCount, x => x > 0.5f ? x : 0.0f, x => 1.0f);
		GenerateBuildings(map, museumPrefabs, museumCount, x => x > 0.5f ? x : 0.0f, x => 1.0f);
		GenerateBuildings(map, buildingPrefabs, buildingCount, x => x, x => 1.0f + Random.Range(0.0f, 6.0f) * x * x * x);
		GenerateBuildings(map, treePrefabs, treeCount, x => 1.0f - x, x => 1.0f);
	}

	private float[,] CreateMap()
	{
		float[,] map = new float[width, height];
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				float v = Mathf.PerlinNoise(0.02f * i + 0.43f, 0.02f * j + 0.22f) * 1.4f - 0.2f;
				map[i, j] = Mathf.Clamp(v * v, 0.0f, 1.0f);
			}
		}
		return map;
	}

	private void GenerateRiver(float[,] map, Color[] pixels)
	{
		Vector3 start = new Vector3(width / 2.0f, 0, 0.0f);
		int i = 0;
		float sectionSize = 2.0f;
		while (start.y < height)
		{
			float size = Mathf.PerlinNoise(i * 0.05f * sectionSize, 500.0f) * 5.0f + 2.5f;
			GameObject river = Instantiate(riverPrefab, transform.position + new Vector3(start.x, 0.0f, start.y), transform.rotation, transform);
			river.transform.localScale = new Vector3(size, 1, size);

			float angle = (Mathf.PerlinNoise(i * 0.05f * sectionSize, 100.0f) - 0.5f) * 300.0f;
			Quaternion q = Quaternion.AngleAxis(angle, Vector3.forward);
			Vector3 end = start + q * Vector3.up * sectionSize;
			DrawLine(pixels, start, end, Color.blue);
			start = end;
			//CreateRiver(left, right);
			i++;
		}
	}

	private void GenerateGraph(int pointCount, Func<float, float> probaFunc, float roadWidth, bool allowBridges, out List<LineSegment> edges, out List<Vector2> intersections)
	{
		/* Create random points points */
		List<Vector2> points = new List<Vector2>();
		List<uint> colors = new List<uint>();

		/* Randomly pick vertices */
		for (int i = 0; i < pointCount; i++)
		{
			colors.Add(0);
			Vector2 vec = RandomPoint(map, probaFunc);
			points.Add(vec);
		}

		Voronoi v = new Voronoi(points, colors, new Rect(0, 0, width, height));

		edges = new List<LineSegment>();
		HashSet<Vertex> vertices = new HashSet<Vertex>();

		// Find edges per vertex
		foreach (Edge e in v.Edges())
		{
			if (!e.visible) continue;

			bool[] onRiver = new bool[2];
			Side[] sides = new[] { Side.LEFT, Side.RIGHT };
			for (int i = 0; i < 2; i++)
			{
				Vector2 pos = e.clippedEnds[sides[i]].Value;
				Vector3 position = transform.position + new Vector3(pos.x, 0, pos.y);
				onRiver[i] = Physics.CheckSphere(position, roadWidth / 2, LayerMask.GetMask("River"), QueryTriggerInteraction.Ignore);
			}

			bool removeEdge = onRiver[0] || onRiver[1];

			if (!removeEdge && !allowBridges)
			{
				// Check if road over water
				Vector3 left = new Vector3(e.clippedEnds[Side.LEFT].Value.x, 0, e.clippedEnds[Side.LEFT].Value.y);
				Vector3 right = new Vector3(e.clippedEnds[Side.RIGHT].Value.x, 0, e.clippedEnds[Side.RIGHT].Value.y);
				Vector3 delta = right - left;
				Vector3 position = transform.position + left;
				Quaternion rotation = transform.rotation * Quaternion.LookRotation(delta);
				Vector3 size = new Vector3(roadWidth, 1.0f, delta.magnitude);
				removeEdge = Physics.CheckBox(position + delta / 2.0f, size / 2.0f, rotation, LayerMask.GetMask("River"), QueryTriggerInteraction.Ignore);
			}

			if (removeEdge)
			{
				if (e.leftVertex != null && !onRiver[0] && Vector2.Distance(e.clippedEnds[Side.LEFT].Value, e.leftVertex.Coord) < 0.001)
					vertices.Add(e.leftVertex);
				if (e.rightVertex != null && !onRiver[1] && Vector2.Distance(e.clippedEnds[Side.RIGHT].Value, e.rightVertex.Coord) < 0.001)
					vertices.Add(e.rightVertex);
			}
			else
			{
				edges.Add(e.VoronoiEdge());
			}
		}

		intersections = vertices.Select(v => v.Coord).ToList();
	}

    private void CreateRoad(Vector3 left, Vector3 right, List<GameObject> roadPrefabList, List<Car> vehiclePrefabList, List<GameObject> bridgePrefabList, int depth = 0)
    {
		Vector3 delta = right - left;
		Vector3 position = transform.position + left;
		Quaternion rotation = transform.rotation * Quaternion.LookRotation(delta);
		Vector3 size = new Vector3(1, delta.magnitude, delta.magnitude);
		GameObject prefab;
		bool bridge = Physics.CheckBox(position + delta / 2.0f, size / 2.0f, rotation, LayerMask.GetMask("River"), QueryTriggerInteraction.Ignore);
		if (bridge)
		{
			if (bridgePrefabList.Count == 0)
				return;

			// Cut road in 3

			if (depth < 4)
			{
				Vector3 p1 = left + delta / 3.0f;
				Vector3 p2 = left + delta * 2.0f / 3.0f;
				bool onRiver1 = Physics.CheckSphere(transform.position + p1, 1.3f / 2, LayerMask.GetMask("River"), QueryTriggerInteraction.Ignore);
				bool onRiver2 = Physics.CheckSphere(transform.position + p2, 1.3f / 2, LayerMask.GetMask("River"), QueryTriggerInteraction.Ignore);

				if (!onRiver1 && !onRiver2)
				{
					CreateRoad(left, p1, roadPrefabList, vehiclePrefabList, bridgePrefabList, depth + 1);
					CreateRoad(p1, p2, roadPrefabList, vehiclePrefabList, bridgePrefabList, depth + 1);
					CreateRoad(p2, right, roadPrefabList, vehiclePrefabList, bridgePrefabList, depth + 1);
					return;
				}
				else if (!onRiver1 && onRiver2)
				{
					CreateRoad(left, p1, roadPrefabList, vehiclePrefabList, bridgePrefabList, depth + 1);
					CreateRoad(p1, right, roadPrefabList, vehiclePrefabList, bridgePrefabList, depth + 1);
					return;
				}
				else if (!onRiver2 && onRiver1)
				{
					CreateRoad(p2, right, roadPrefabList, vehiclePrefabList, bridgePrefabList, depth + 1);
					CreateRoad(left, p2, roadPrefabList, vehiclePrefabList, bridgePrefabList, depth + 1);
					return;
				}
			}

			prefab = bridgePrefabList[Random.Range(0, bridgePrefabList.Count)];
		}
		else
		{
			prefab = roadPrefabList[Random.Range(0, roadPrefabList.Count)];
		}
		GameObject road = Instantiate(prefab, position, rotation, transform);
		road.transform.localScale = size;

		for (int i = 0; i < road.transform.childCount; i++)
        {
			Transform child = road.transform.GetChild(i);
			Vector3 childScale = child.localScale;
			childScale.y /= delta.magnitude;
			child.localScale = childScale;
		}

		if (!bridge && vehiclePrefabList.Count != 0)
        {
			Car car = Instantiate(vehiclePrefabList[Random.Range(0, vehiclePrefabList.Count)], transform.position + left + delta / 2 + Vector3.up * 0.1f, Quaternion.identity);
			car.City = this;
        }

		if (ambulance == null)
		{
			ambulance = Instantiate(ambulancePrefab, transform.position + left + delta / 3 + Vector3.up * 0.1f, Quaternion.identity);
			ambulance.City = this;
		}
		else if (police == null)
		{
			police = Instantiate(policePrefab, transform.position + left + 5 * delta / 6 + Vector3.up * 0.1f, Quaternion.identity);
			police.City = this;
		}
	}

	private void GenerateBuildings(float[,] map, List<Building> prefabList, int count, Func<float, float> probaFunc, Func<float, float> verticalScale)
	{
		for (int b = 0; b < count; b++)
		{
			Building prefab = prefabList[Random.Range(0, prefabList.Count)];
			Vector2 point;
			Vector3 pos;
			Vector3 scale;
			Vector3 size;
			Quaternion rotation;
			do
			{
				point = RandomPoint(map, probaFunc); // x => 1 - 2 * Math.Abs(x - 0.5f));
				pos = transform.position + new Vector3(point.x, 0, point.y);
				float angle = Random.Range(0.0f, 360.0f);
				rotation = transform.rotation * Quaternion.AngleAxis(angle, Vector3.up);
				float v = map[(int)point.x, (int)point.y];
				scale = new Vector3(1, verticalScale(v), 1);
				size = prefab.collisionArea.size;
				size.Scale(scale);
			}
			while (Physics.CheckBox(pos + rotation * (prefab.collisionArea.min + size / 2), size / 2, rotation, LayerMask.GetMask("Default", "Road", "River"), QueryTriggerInteraction.Ignore));
			Building building = Instantiate(prefab, pos, rotation, transform);
			building.transform.localScale = scale;
		}
	}

	private Vector2 RandomPoint(float[,] map)
	{
    	return RandomPoint(map, x=>x);
	}

    private Vector2 RandomPoint(float[,] map, Func<float, float> probaFunc)
	{
		List<Tuple<Vector2, float>> candidates = new List<Tuple<Vector2, float>>();
		for (int i = 0; i < 256; i++)
        {
			Vector2 pos = new Vector2(Random.Range(0, width), Random.Range(0, height));
			candidates.Add(Tuple.Create(pos, probaFunc(map[(int)pos.x, (int)pos.y])));
		}

		float totalWeight = candidates.Sum(tuple => tuple.Item2);

		float value = Random.Range(0, totalWeight);

		for (int i = 0; i < 256; i++)
        {
			if (value < candidates[i].Item2)
            {
				return candidates[i].Item1;
			}
			value -= candidates[i].Item2;
		}

		return candidates[candidates.Count - 1].Item1;
	}

	private Color step(float intensity)
	{
		Color[] colors = new []{
			new Color(0.15f, 0.85f, 0.15f, 1.0f),
			new Color(0.55f, 0.85f, 0.1f, 1.0f),
			Color.grey,
			new Color(0.3f, 0.3f, 0.3f, 1.0f)
		};
		int color_index = (int) Mathf.Round(3*Mathf.Sqrt(intensity));

		return colors[color_index];
	}

    /* Functions to create and draw on a pixel array */
    private Color[] CreatePixelMap(float[,] map)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
            {
                pixels[i + j * width] = step(map[i, j]);
            }
        return pixels;
    }
    private void DrawPoint(Color[] pixels, Vector2 p, Color c) {
		if (p.x < width && p.x >= 0 && p.y < height && p.y >= 0) 
		    pixels[(int)p.x + (int)p.y * width]=c;
	}
	// Bresenham line algorithm
	private void DrawLine(Color [] pixels, Vector2 p0, Vector2 p1, Color c) {
		int x0 = (int)p0.x;
		int y0 = (int)p0.y;
		int x1 = (int)p1.x;
		int y1 = (int)p1.y;

		int dx = Mathf.Abs(x1-x0);
		int dy = Mathf.Abs(y1-y0);
		int sx = x0 < x1 ? 1 : -1;
		int sy = y0 < y1 ? 1 : -1;
		int err = dx-dy;
		while (true) {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
    			pixels[x0 + y0 * width] = c;

			if (x0 == x1 && y0 == y1) break;
			int e2 = 2 * err;
			if (e2 > -dy) {
				err -= dy;
				x0 += sx;
			}
			if (e2 < dx) {
				err += dx;
				y0 += sy;
			}
		}
	}

	public void CallAmbulance(Car car)
	{
		ambulance.CallAmbulance(car);
	}

	public void CallPolice(Car car)
	{
		police.CallPolice(car);
	}

	public void StopAmbulance(Car car)
	{
		ambulance.StopAmbulance(car);
	}

	public void StopPolice(Car car)
	{
		police.StopPolice(car);
	}
}