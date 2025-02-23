export interface TaskItem {
  Id: number;
  Title: string;
  Description: string;
  Closed: boolean;
  Priority: string;
  DueDate: string;
  CompletionDate: string;
  AssignedTo: string;
  CompletedBy: string;
  CompletionNotes: string;
  TaskLink: string;
}

export interface User {
  id: string;
  displayName: string;
  email: string;
}