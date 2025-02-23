import { AadHttpClient, HttpClient } from "@microsoft/sp-http";

import "@pnp/sp/webs";
import "@pnp/sp/lists";
import "@pnp/sp/search";
import "@pnp/sp/webs";
import "@pnp/sp/lists";
import "@pnp/sp/items";
import "@pnp/sp/webs";
import "@pnp/sp/lists";
import "@pnp/sp/items";
import "@pnp/graph/users";
import "@pnp/sp/site-users/web";
import { WebPartContext } from "@microsoft/sp-webpart-base";
import { CompleteReadReceiptTaskResponse } from "../../../models/completeReadReceiptTask";
import { escape } from "@microsoft/sp-lodash-subset";

export class ReadReceiptTaskService {
  

  constructor(private context: WebPartContext,
    private apiUrl: string,
    private apiKey: string
    //private apiResourceId: string
  ) {
    /*
    this.context.aadHttpClientFactory.getClient(apiResourceId).then((client) => {
        // prove that we can create a client.
        console.log("client", client);
    });
    */
  }

  public constructApiUrl = (apiFragment: string): string => {
    let requestUrl = `${this.apiUrl}/${apiFragment}`;
    if(this.apiKey && this.apiKey.length > 0){
      requestUrl = requestUrl.concat(`?code=${this.apiKey}`);
    }
    return requestUrl;
  }

  public getPlannerTask = async (taskId: string): Promise<CompleteReadReceiptTaskResponse> => {
      let result: CompleteReadReceiptTaskResponse = new CompleteReadReceiptTaskResponse();
      const escapedTaskId = escape(taskId);
      const requestUrl = this.constructApiUrl(`tasks/search/${escapedTaskId}`);
            
      try {
        const response = await this.context.httpClient.get(requestUrl, HttpClient.configurations.v1, {headers: {'Accept':'application/json'}});
        console.log("response", response);
        result = await response.json();
        return result;
      }
      catch(error){
        console.error("Error getting planner task: ", error);
        throw error;
      }
  }

  public updatePlannerTask = async (taskId: string, task: CompleteReadReceiptTaskResponse): Promise<CompleteReadReceiptTaskResponse> => {
    let result: CompleteReadReceiptTaskResponse = new CompleteReadReceiptTaskResponse();
    const escapedTaskId = escape(taskId);
    const requestUrl = this.constructApiUrl(`tasks/${escapedTaskId}`);

    try {
      const response = await this.context.httpClient.post(requestUrl, AadHttpClient.configurations.v1, 
        {headers: {'Accept': 'application/json', 'Content-Type': 'application/json' }, 
        body: JSON.stringify(task)});
      result = await response.json();
      return result;
    
    }
    catch(error){
      console.error("Error updating planner task: ", error);
      throw error;
    }

   
  }

  public completePlannerTask = async (taskId: string, task: CompleteReadReceiptTaskResponse): Promise<CompleteReadReceiptTaskResponse> => {
    let result: CompleteReadReceiptTaskResponse = new CompleteReadReceiptTaskResponse();
    const escapedTaskId = escape(taskId);
    const requestUrl = this.constructApiUrl(`tasks/${escapedTaskId}/complete`);

    try {
    const response = await this.context.httpClient.post(requestUrl, AadHttpClient.configurations.v1, 
      {headers: {'Accept': 'application/json', 'Content-Type': 'application/json' }, 
      body: JSON.stringify(task)});
      result = await response.json();
      return result;
    }
    catch(error){
      console.error("Error completing planner task: ", error);
      throw error;
    }
  }

 
}