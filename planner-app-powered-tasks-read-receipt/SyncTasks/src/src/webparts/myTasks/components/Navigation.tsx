import * as React from 'react';
import { INavigationProps } from './INavigationProps';
import { Button } from '@fluentui/react-components';
import styles from './MyTasks.module.scss';

export default class Navigation extends React.Component<INavigationProps, {}> {

  public render(): React.ReactElement<INavigationProps> {

    const {
      setView,
      setIsChecked,
      view,
      manager, 
      refresh,
      setRefresh
    } = this.props;

    return (
      <div className={styles.navigation}>
          <div>        
            <Button className={styles.pillButton} appearance={(view === 'ToDo' || view === 'Complete') ? 'primary' : 'secondary'} onClick={() => {
          setView('ToDo')
          setIsChecked(false)
        }}>My Tasks</Button>
        {manager && (
          <>
            <Button className={styles.pillButton}  appearance={(view === 'ToDoStaff' || view === 'CompleteStaff') ? 'primary' : 'secondary'} onClick={() => {
              setView('ToDoStaff')
              setIsChecked(false)
            }}>My Teams Tasks</Button>
          </>
        )}</div>
        <div>
          <Button className={styles.pillButton} appearance="primary" onClick={() => {
            setRefresh(!refresh)
          }}> Refresh</Button>
        </div>
      </div>
    );
  }
}

