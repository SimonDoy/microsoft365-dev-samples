export class CompleteReadReceiptTaskResponse {
    id: string = "";
    externalId:string = "";
    description:string = "";
    userPrincipalName:string = "";
    understandingLevel:string = "";
    hasReadContent:boolean = false;
    confirmationDate:Date = new Date();
    completionNotes:string = "";
    percentComplete:number = 0;
    contentTitle = "";
    contentUrl = "";
}