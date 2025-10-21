# Follow-up Plan — ElastiCache Stress Testing and Monitoring

## Objective
Finalize and validate the **CacheStressTester** utility to meet all Acceptance Criteria (AC #2–#5) by enabling metric publishing, alert configuration, and result analysis for AWS ElastiCache and Azure Redis.

---

## Phase 1 — Metrics Streaming & Monitoring Enhancement  
*(Covers AC #2 – Monitoring Implementation)*  

- [ ] **1.1 Implement Periodic Metrics Push**  
  Add a background task in `CacheStressRunner` or `MetricsPublisherService` to push metrics every N seconds while the test runs.

- [ ] **1.2 Enable Environment-Based Publisher Selection**  
  Automatically select **CloudWatch** (for AWS) or **Azure Monitor** (for Azure) using `Environment` config.

- [ ] **1.3 Verify Metric Mappings**  
  Map Redis INFO fields → CloudWatch custom metrics (`UsedMemoryMB`, `HitRatio`, `OpsPerSec`).

- [ ] **1.4 Test with Local Redis & Dry-Run Publish**  
  Simulate metric push locally to validate payload format and SDK calls.

---

## Phase 2 — Alerting & Thresholds  
*(Covers AC #3 – Automated Notifications)*  

- [ ] **2.1 Define Threshold Levels**  
  Set alerts for memory > 70 %, latency > 500 ms, timeouts > 5 %.

- [ ] **2.2 Configure CloudWatch Alarms**  
  Use AWS Console or SDK to create alarms on custom metrics (`CacheStress/UsedMemoryMB`, `CacheStress/Latency`).

- [ ] **2.3 (Optional) Azure Monitor Alerts**  
  Replicate thresholds for Azure Cache layer when ready to test.

- [ ] **2.4 Verify Notification Flow**  
  Confirm alerts trigger (Slack / email / SNS) under simulated conditions.

---

## Phase 3 — Results Documentation & Sharing  
*(Covers AC #4 – Results Shared with Teams)*  

- [ ] **3.1 Enhance `ResultReporter`**  
  Add export to Markdown and/or CSV summaries using `TestResults_Summary_Template.md`.

- [x] **3.2 Generate Standardized Run Reports**  
  Store outputs under `/Reports` with naming convention `result_{env}_{timestamp}.json`.

- [ ] **3.3 Attach Sample Reports to Story**  
  Upload 2–3 executions (AWS, Azure, Local) to Azure DevOps attachments.

- [ ] **3.4 Create Consolidated Summary**  
  Use the provided summary template to highlight findings, latency patterns, and recommendations.

---

## Phase 4 — Scaling Recommendations & Validation  
*(Covers AC #5 – Recommendations for Scaling or Config Changes)*  

- [ ] **4.1 Analyze Collected Metrics**  
  Compare memory growth, hit ratio, and timeouts to baseline.

- [ ] **4.2 Propose EPCU Adjustments**  
  If latency spikes with load, recommend increasing `min EPCU`.

- [ ] **4.3 Consider Managed ElastiCache**  
  If scaling lags persist, suggest migration to fully managed mode.

- [ ] **4.4 Prepare Summary for Next Stand-up**  
  Present findings and final recommendations to the platform team.

---

## Completion Criteria for Story Closure
- [ ] Stress test utility executed successfully on AWS and Azure.  
- [ ] Metrics collected and published to CloudWatch/Azure Monitor.  
- [ ] Alert thresholds validated and triggered as expected.  
- [ ] Summary reports shared with team (including recommendations).  
- [ ] Team confirms no further cache-related timeouts after tuning.
