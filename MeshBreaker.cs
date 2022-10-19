namespace takashicompany.Unity
{
	using System.Collections;
	using System.Collections.Generic;
	using UnityEngine;
	using BLINDED_AM_ME;

	public class MeshBreaker : MonoBehaviour
	{
		[SerializeField, Header("割るメッシュを持ったレンダラー")]
		private Renderer _target;

		[SerializeField, Header("砕けた破片達の親階層。指定しない場合は_targetと同階層になる")]
		private Transform _pieceRoot;

		[SerializeField, Header("Awake時に事前にメッシュを割っておくオプション")]
		private bool _preBreakOnAwake = false;

		[SerializeField, Header("割った切断面に充てるマテリアル")]
		private Material _capMaterial;

		[SerializeField, Header("割った結果生成されたメッシュ")]
		private Renderer[] _pieces;

		[SerializeField, Header("分割する世代数。多いほど細かくなる(そして処理が重くなる)")]
		private int _breakLevel = 3;

		private void Awake()
		{
			if (_preBreakOnAwake)
			{
				Break();
			}
		}
		
		/// <summary>
		/// 既に割られているか
		/// </summary>
		/// <returns></returns>
		public bool IsBroken()
		{
			return _pieces != null && _pieces.Length > 0;
		}

		/// <summary>
		/// メッシュを割る
		/// </summary>
		/// <returns></returns>
		public List<Renderer> Break()
		{
			var list = new List<Renderer>();

			if (IsBroken())
			{
				list.AddRange(_pieces);
				return list;
			}

			var position = _target.transform.position;

			_pieces = Break(_target, _breakLevel, _pieceRoot != null ? _pieceRoot : transform.parent, _capMaterial);
			list.AddRange(_pieces);

#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				//var so = new UnityEditor.SerializedObject(this);
				for (int i = 0; i < _pieces.Length; i++)
				{
					var mesh = _pieces[i].GetComponent<MeshFilter>().sharedMesh;
					if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/BrokenMeshes"))
					{
						UnityEditor.AssetDatabase.CreateFolder("Assets", "BrokenMeshes");
					}
					UnityEditor.AssetDatabase.CreateAsset(mesh, "Assets/BrokenMeshes/" + System.Guid.NewGuid() + ".asset");
					
					// var so = new UnityEditor.SerializedObject(_pieces[i].GetComponent<MeshFilter>());
					// so.FindProperty("m_Mesh").objectReferenceValue = mesh;
					// so.Update();
					// so.ApplyModifiedProperties();
					
					_pieces[i].GetComponent<MeshFilter>().sharedMesh = mesh;

					UnityEditor.EditorUtility.SetDirty(_pieces[i].GetComponent<MeshFilter>());
				}

				UnityEditor.EditorUtility.SetDirty(this);
				UnityEditor.EditorUtility.SetDirty(this.gameObject);
				
				UnityEditor.AssetDatabase.SaveAssets();
				
			}
#endif
			return list;
		}

		/// <summary>
		/// メッシュを割りつつ爆散させる
		/// </summary>
		/// <param name="point">爆散させる時の中心点</param>
		/// <param name="force">爆散させた時の力の量</param>
		/// <param name="radius">爆散が及ぶ範囲</param>
		/// <returns></returns>
		public List<Renderer> BreakAndExplode(Vector3 point, float force, float radius)
		{
			var pieces = Break();

			var rigidbodies = new List<Rigidbody>();

			foreach (var renderer in pieces)
			{
				var rigidbody = renderer.gameObject.AddComponent<Rigidbody>();
				var collider = renderer.gameObject.AddComponent<MeshCollider>();
				collider.convex = true;
				renderer.gameObject.layer = gameObject.layer;
				var center = renderer.bounds.center;

				// var force = (center - point).normalized * power;

				rigidbody.AddExplosionForce(force, point, radius);

				// var torque = new Vector3(
				// 	Random.Range(-1, 1f),
				// 	Random.Range(-1f, 1f),
				// 	Random.Range(-1f, 1f)
				// ).normalized;

				// rigidbody.AddTorque(torque * 500);

				rigidbodies.Add(rigidbody);
			}

			return pieces;
		}


#region static関数

		/// <summary>
		/// メッシュを割る
		/// </summary>
		/// <param name="target">割る対象のメッシュを持つレンダラー</param>
		/// <param name="maxGeneration">世代数。多いほど細かく分割される</param>
		/// <param name="piecesParent">割ったメッシュを格納するTransform</param>
		/// <param name="capMaterial">切断面にあてるマテリアル</param>
		/// <returns></returns>
		public static Renderer[] Break(
			Renderer target,
			int maxGeneration,
			Transform piecesParent,
			Material capMaterial = null
		)
		{
			if (capMaterial == null)
			{
				capMaterial = target.material;
			}

			var position = target.transform.position;

			var hashSet = new HashSet<Renderer>();

			CutOnce(target.transform, target, capMaterial, maxGeneration, 0, hashSet);

			List<Renderer> pieces = new List<Renderer>();

			foreach (var renderer in hashSet)
			{
				var center = renderer.bounds.center;
				renderer.transform.SetParent(piecesParent);

				var meshFilter = renderer.GetComponent<MeshFilter>();

				var mesh = Application.isPlaying ? meshFilter.mesh : meshFilter.sharedMesh;

				var offset = center - renderer.transform.position;

				var verts = mesh.vertices;

				for (int i = 0; i < mesh.vertices.Length; i++)
				{
					var p = mesh.vertices[i];
					p -= renderer.transform.InverseTransformVector(offset);
					verts[i] = p;
				}

				mesh.vertices = verts;

				renderer.transform.position = center;

				pieces.Add(renderer);
			}

			return pieces.ToArray();
		}

		private static void CutOnce(
			Transform root,
			Renderer target,
			Material capMaterial,
			int maxGeneration,
			int generation,
			HashSet<Renderer> hashSet
		)
		{
			if (generation >= maxGeneration)
			{
				return;
			}
			
			var beforeCutPlane = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;

			beforeCutPlane = target.transform.rotation * beforeCutPlane;

			var targets = MeshCut.Cut(target.gameObject, target.bounds.center, beforeCutPlane, capMaterial);

			generation++;

			hashSet.Remove(target);

			foreach (var t in targets)
			{
				if (t == null)
				{
					continue;
				}

				var r = t.GetComponent<Renderer>();

				if (r != null)
				{
					hashSet.Add(r);
					
					// もう一度呼ぶ
					CutOnce(root, r, capMaterial, maxGeneration, generation, hashSet);
				}
			}
		}
#endregion
	}

	public static class MeshBreakerExtensions
	{
		public static List<Rigidbody> Explode(this IEnumerable<Renderer> targets, Vector3 point, float force, float radius)
		{
			var rigidbodies = new List<Rigidbody>();

			foreach (var renderer in targets)
			{
				var rigidbody = renderer.gameObject.AddComponent<Rigidbody>();
				var collider = renderer.gameObject.AddComponent<MeshCollider>();
				collider.convex = true;
				var center = renderer.bounds.center;

				// var force = (center - point).normalized * power;

				rigidbody.AddExplosionForce(force, point, radius);

				// var torque = new Vector3(
				// 	Random.Range(-1, 1f),
				// 	Random.Range(-1f, 1f),
				// 	Random.Range(-1f, 1f)
				// ).normalized;

				// rigidbody.AddTorque(torque * 500);

				rigidbodies.Add(rigidbody);
			}

			return rigidbodies;
		}
	}
}