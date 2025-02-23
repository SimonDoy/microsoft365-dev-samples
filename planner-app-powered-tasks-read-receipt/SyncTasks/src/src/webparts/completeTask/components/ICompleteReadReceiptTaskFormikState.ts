export interface ICompleteReadReceiptTaskFormikState {
    externalId: string;
    userPrincipalName?: string;
    understandingLevel?: string;
    hasReadContent:boolean;
    confirmationDate:Date;
    completionNotes:string;
  }