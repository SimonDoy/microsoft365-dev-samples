

import * as React from 'react';
import * as ReactDom from 'react-dom';
import { Version } from '@microsoft/sp-core-library';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';
import { spfi, SPFx } from "@pnp/sp";
import { graphfi, SPFx as graphSPFx } from "@pnp/graph";
import "@pnp/sp/webs";

import CompleteReadReceiptTask from './components/CompleteReadReceiptTask';
import { ReadReceiptTaskService } from '../myTasks/service/readReceiptTaskService';

export interface ICompleteTasksWebPartProps {
  description: string;
}
// WebPart Class
export default class CompleteTasksWebPart extends BaseClientSideWebPart<ICompleteTasksWebPartProps> {
  private _taskService: ReadReceiptTaskService;
  private isDevelopmentEnvironment = true;
  private apiUrl: string = "";
  private apiKey: string = "";
  //private apiResourceId: string = "";

  // Set up the component state using React's state hooks
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private _setComponentState: React.Dispatch<React.SetStateAction<any>> | undefined;
  private _taskId: string = "";
  public async render(): Promise<void> {

    if(this.isDevelopmentEnvironment && this.isDevelopmentEnvironment === true){
      this.apiUrl = "http://localhost:7165/api";
      this.apiKey = "[Insert Function Code Key]"; // when running local set to blank
    }

    this._taskService = new ReadReceiptTaskService(this.context, this.apiUrl, this.apiKey);
    const element: React.ReactElement = React.createElement(
      CompleteReadReceiptTask,
      {
        taskService: this._taskService,
        taskId: this._taskId
      }
    );
  
    ReactDom.render(element, this.domElement);
  }
  
  public async onInit(): Promise<void> {
    await super.onInit();
    const sp = spfi().using(SPFx(this.context));
    const graph = graphfi().using(graphSPFx(this.context));
    
    const teamsContext = await this.context.sdks.microsoftTeams?.teamsJs.app.getContext();
    // get query string from the URL find subEntityId
    const queryString = new URLSearchParams(window.location.search);
    this._taskId = queryString.get("subEntityId") as string;

    console.log("teamsContext", teamsContext);
    if(teamsContext?.page.subPageId){
      // load task information.
      this._taskId = teamsContext.page.subPageId;
    }
    
    // Fetch data asynchronously
    this._fetchData().then().catch(e=>console.error(e));
  }

  // Fetch data and update state
  private async _fetchData(): Promise<void>  {
    try {
      // Update the tasks and loading state after fetching
      if (this._setComponentState) {
        this._setComponentState({ 
          loading: false 
        });
      }
    } catch (error) {
      console.error("Error fetching tasks: ", error);
      if (this._setComponentState) {
        this._setComponentState({ loading: false });
      }
    }
  }

  public connectedCallback():void {
    this._setComponentState = React.useState({
      loading: true,
    })[1];
  }

  protected onDispose(): void {
    ReactDom.unmountComponentAtNode(this.domElement);
  }

  protected get dataVersion(): Version {
    return Version.parse('1.0');
  }
}
