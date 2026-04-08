using Unity.MLAgents;
using UnityEngine;

[RequireComponent(typeof(SimplifiedCoverage))]
public class MetricsTracker : MonoBehaviour
{
    private SimplifiedCoverage _agent;
    private GridCoverageTracker2D _coverage;

    private void Awake()
    {
        _agent = GetComponent<SimplifiedCoverage>();
        _coverage = _agent.coverage;
    }

    private void FixedUpdate()
    {
        if (_coverage == null) return;

        float coveragePercent = _coverage.GetCoverage01() * 100f;

        Academy.Instance.StatsRecorder.Add(
            "custom/coverage_percent",
            coveragePercent,
            StatAggregationMethod.MostRecent
        );
    }
}