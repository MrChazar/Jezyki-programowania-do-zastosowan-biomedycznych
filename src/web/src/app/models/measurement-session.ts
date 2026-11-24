export interface MeasurementSession {
  id: string;
  name: string;
  deviceId: string | null;
  source: string;
  startedAtUtc: string;
  finishedAtUtc: string | null;
  tags: string[];
}

export interface HeartRateMeasurement {
  id: string;
  sessionId: string | null;
  deviceId: string | null;
  measuredAtUtc: string;
  bpm: number;
  intervalSeconds: number;
}

export interface MeasurementDate {
  date: string;
  measurementIds: string[];
}

export interface HeartRateStatistics {
  measurementCount: number;
  minimumBpm: number | null;
  maximumBpm: number | null;
  averageBpm: number | null;
  peakAtUtc: string | null;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
