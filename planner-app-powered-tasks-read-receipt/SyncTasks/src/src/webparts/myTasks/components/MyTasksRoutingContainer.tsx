import * as React from 'react';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import MyTasksContainer from './MyTasksContainer';

interface MyTasksRoutingContainerProps {
  
}

const MyTasksRoutingContainer: React.FC<MyTasksRoutingContainerProps> = () => {
  
  return (
      <BrowserRouter>
        <Routes>
          <Route path='*' element={<MyTasksContainer />} />
        </Routes>
      </BrowserRouter>
    );
};

export default MyTasksRoutingContainer;