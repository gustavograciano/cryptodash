export interface NewsItemDto {
  id: number;
  title: string;
  url: string;
  source: string;
  publishedAt: string;
  coinsMentioned: string[];
  sentiment: string | null;
}

export interface AiDigestDto {
  id: number;
  date: string;
  summaryMd: string;
  coinsCovered: string[];
  model: string;
  createdAt: string;
}
