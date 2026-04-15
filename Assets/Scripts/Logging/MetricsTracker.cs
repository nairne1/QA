using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

[RequireComponent(typeof(SimplifiedCoverage))]
//tracks and logs coverage percentage and other metrics to ML-Agents StatsRecorder
public class MetricsTracker : MonoBehaviour
{
    //references to agent and coverage tracker
    private SimplifiedCoverage _agent;
    private GridCoverageTracker2D _coverage;

    //variables to track coverage history for last 100 episodes
    private int _lastEpisode = -1;
    private float _lastCoveragePercent = 0f;

    private readonly Queue<float> _last100Coverage = new Queue<float>();
    private float _coverageSum = 0f;

    //initialize references
    private void Awake()
    {
        _agent = GetComponent<SimplifiedCoverage>();
        _coverage = _agent.coverage;
    }

    private void FixedUpdate()
    {
        //if references are missing, skip logging
        if (_agent == null || _coverage == null)
            return;

        //get current coverage percentage from the agent
        float currentCoveragePercent = _agent.CoveragePercent;

        //if the episode changed, push the previous episode's final coverage
        if (_lastEpisode != -1 && _agent.CurrentEpisode != _lastEpisode)
        {
            AddCoverageToHistory(_lastCoveragePercent);
        }

        //update last episode and coverage for the next update
        _lastEpisode = _agent.CurrentEpisode;
        _lastCoveragePercent = currentCoveragePercent;

        //log current coverage percentage and other metrics to ML-Agents StatsRecorder
        Academy.Instance.StatsRecorder.Add(
            "custom/coverage_percent",
            currentCoveragePercent,
            StatAggregationMethod.MostRecent
        );

        Academy.Instance.StatsRecorder.Add(
            "custom/coverage_percent_last_100",
            GetAverageCoverageLast100(),
            StatAggregationMethod.MostRecent
        );

        Academy.Instance.StatsRecorder.Add(
            "custom/deaths_this_episode",
            _agent.DeathCount,
            StatAggregationMethod.MostRecent
        );

        Academy.Instance.StatsRecorder.Add(
            "custom/bugs_found_this_episode",
            _agent.BugCount,
            StatAggregationMethod.MostRecent
        );
    }

    //helper methods to maintain coverage history for last 100 episodes
    private void AddCoverageToHistory(float coveragePercent)
    {
        _last100Coverage.Enqueue(coveragePercent);
        _coverageSum += coveragePercent;

        if (_last100Coverage.Count > 100)
        {
            _coverageSum -= _last100Coverage.Dequeue();
        }
    }

    private float GetAverageCoverageLast100()
    {
        if (_last100Coverage.Count == 0)
            return 0f;

        return _coverageSum / _last100Coverage.Count;
    }
}