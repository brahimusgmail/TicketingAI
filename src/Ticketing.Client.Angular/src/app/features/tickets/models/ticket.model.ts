export interface Ticket {
  id: string;
  title: string;
  authorId: string | null;
  categoryId: string | null;
  statut: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  comments: unknown[];
}
