import * as React from 'react';
import { Spinner } from '@fluentui/react/lib/Spinner';
import {  FluentProvider,  webLightTheme } from '@fluentui/react-components';
import styles from './MyTasks.module.scss';


interface MyTasksContainerProps {
  
}

const MyTasksContainer: React.FC<MyTasksContainerProps> = () => {
  const [loading, setLoading] = React.useState(true);
  const [refresh, setRefresh]= React.useState(true)
  React.useEffect(() => {

    const fetchData = async (): Promise<void>  => {
      setLoading(true)
      try {
        // load some stuff.
        setRefresh(true);
      } catch (error) {
        console.error("Error loading tasks: ", error);
      } finally {
        setLoading(false); // Set loading to false when done
      }
    };

     fetchData().then().catch(e=>console.error(e));

  }, [refresh]); 

  return (
      <FluentProvider theme={webLightTheme}>
        <div className={styles.container}>

          {loading ? (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
              <Spinner label="Loading tasks..." ariaLive="assertive" labelPosition="right" />
            </div>
          ) : (
            <div> This is part of a demo on Microsoft Planner App-Powered Tasks. See www.simondoy.com for more information. </div>
          )}
        </div>
      </FluentProvider>
    );
};

export default MyTasksContainer;
