using System.Collections.Generic;
using UnityEngine;

// 플레이어 몸 포즈(스케일 y) 전담 컴포넌트.
// 숙이기/웅크리기/무릎꿇기는 전부 여기서만 건드린다.
//
// 사용법: PushPose(key, scaleY) / PopPose(key).
// 동시에 여러 요청이 있으면 가장 많이 숙인 값(최소값)이 적용되므로
// 대시 중 피격처럼 겹치는 연출도 깨지지 않는다.
// x(좌우 뒤집힘)는 PlayerMove/PlayerAttack이 관리하므로 건드리지 않는다.
public class PlayerBody : MonoBehaviour
{
    private float baseScaleY = 1f;
    private readonly Dictionary<string, float> poses = new Dictionary<string, float>();

    private void Awake()
    {
        float y = Mathf.Abs(transform.localScale.y);
        baseScaleY = y > 0.001f ? y : 1f;
    }

    private void LateUpdate()
    {
        float scaleY = baseScaleY;
        if (poses.Count > 0)
        {
            float min = 1f;
            foreach (float v in poses.Values)
            {
                if (v < min)
                    min = v;
            }
            scaleY = baseScaleY * Mathf.Clamp(min, 0.2f, 1.2f);
        }

        Vector3 s = transform.localScale;
        if (!Mathf.Approximately(s.y, scaleY))
        {
            s.y = scaleY;
            transform.localScale = s;
        }
    }

    public void PushPose(string key, float scaleY)
    {
        poses[key] = scaleY;
    }

    public void PopPose(string key)
    {
        poses.Remove(key);
    }

    public bool HasPose(string key)
    {
        return poses.ContainsKey(key);
    }

    private void OnDisable()
    {
        poses.Clear();
        Vector3 s = transform.localScale;
        s.y = baseScaleY;
        transform.localScale = s;
    }
}
