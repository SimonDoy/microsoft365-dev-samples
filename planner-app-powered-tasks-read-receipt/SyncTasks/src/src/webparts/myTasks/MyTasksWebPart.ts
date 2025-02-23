

import * as React from 'react';
import * as ReactDom from 'react-dom';
import { Version } from '@microsoft/sp-core-library';
import { BaseClientSideWebPart } from '@microsoft/sp-webpart-base';

import MyTasksRoutingContainer from './components/MyTasksRoutingContainer';
export interface IMyTasksWebPartProps {
  description: string;
}
// WebPart Class
export default class MyTasksWebPart extends BaseClientSideWebPart<IMyTasksWebPartProps> {
 
  public render(): void {
    const element: React.ReactElement = React.createElement(
      MyTasksRoutingContainer,
      {
      }
    );
  
    ReactDom.render(element, this.domElement);
  }
  
  public async onInit(): Promise<void> {
    await super.onInit();

  }

  protected onDispose(): void {
    ReactDom.unmountComponentAtNode(this.domElement);
  }

  protected get dataVersion(): Version {
    return Version.parse('1.0');
  }
}
